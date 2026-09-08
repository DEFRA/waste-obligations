using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Utils.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Services.OrganisationObligations;

public class OrganisationObligationHistoricalBackfillWorker(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<OrganisationObligationHydrationOptions> options,
    ILogger<OrganisationObligationHistoricalBackfillWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processedCount = 0;

            try
            {
                processedCount = await Process(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Organisation obligation historical backfill failed");
            }

            if (processedCount >= options.Value.BatchSize)
                continue;

            await Task.Delay(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds), stoppingToken);
        }
    }

    private async Task<int> Process(CancellationToken stoppingToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IOrganisationObligationHistoricalBackfillStore>();
        var backfill = await store.GetNextIncomplete(stoppingToken);
        if (backfill is null)
            return 0;

        var leaseService =
            scope.ServiceProvider.GetRequiredService<IOrganisationObligationHistoricalBackfillLeaseService>();
        var hydrationService = scope.ServiceProvider.GetRequiredService<IOrganisationObligationHydrationService>();
        var metrics = scope.ServiceProvider.GetRequiredService<IOrganisationObligationHydrationMetrics>();
        var currentObligationYearProvider = scope.ServiceProvider.GetRequiredService<ICurrentObligationYearProvider>();
        var leaseDuration = TimeSpan.FromSeconds(options.Value.LeaseDurationSeconds);
        if (!await leaseService.TryAcquire(leaseDuration, stoppingToken))
        {
            await store.MarkDeferred(
                backfill,
                OrganisationObligationHistoricalBackfillDeferralReason.LeaseHeld,
                stoppingToken
            );
            metrics.LeaseNotAcquired();
            logger.LogInformation(
                "Organisation obligation historical backfill for obligation year {ObligationYear} is deferred because another instance holds its lease",
                backfill.ObligationYear
            );

            return 0;
        }

        using var hydrationCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        using var renewalCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var renewalTask = RenewLease(
            leaseService,
            leaseDuration,
            hydrationCancellationTokenSource,
            renewalCancellationTokenSource.Token
        );

        try
        {
            var currentHydratedCount = 0;
            if (options.Value.PollingEnabled)
            {
                var currentObligationYear = currentObligationYearProvider.GetCurrentObligationYear();
                currentHydratedCount = await hydrationService.HydrateDue(
                    currentObligationYear,
                    hydrationCancellationTokenSource.Token,
                    maximumWork: options.Value.BatchSize
                );
                if (currentHydratedCount > 0)
                {
                    await store.MarkDeferred(
                        backfill,
                        OrganisationObligationHistoricalBackfillDeferralReason.CurrentYearWorkDue,
                        hydrationCancellationTokenSource.Token
                    );
                    logger.LogInformation(
                        "Organisation obligation historical backfill for obligation year {ObligationYear} is deferred because {CurrentYearHydratedCount} current-year work items were due",
                        backfill.ObligationYear,
                        currentHydratedCount
                    );

                    return currentHydratedCount;
                }
            }

            await store.ClearDeferral(backfill, hydrationCancellationTokenSource.Token);
            var progress = await hydrationService.HydrateHistoricalBackfill(
                backfill,
                hydrationCancellationTokenSource.Token,
                maximumWork: options.Value.PollingEnabled ? 1 : options.Value.BatchSize,
                preserveCurrentYearPacing: options.Value.PollingEnabled
            );
            if (progress.RemainingCount == 0)
                await store.Complete(backfill, hydrationCancellationTokenSource.Token);

            logger.LogInformation(
                "Organisation obligation historical backfill processed {ProcessedCount} work items with {RemainingCount} remaining for obligation year {ObligationYear}",
                progress.ProcessedCount,
                progress.RemainingCount,
                backfill.ObligationYear
            );

            return currentHydratedCount + progress.ProcessedCount;
        }
        catch (OperationCanceledException exception)
            when (hydrationCancellationTokenSource.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(
                exception,
                "Organisation obligation historical backfill stopped because its lease was not renewed"
            );

            return 0;
        }
        finally
        {
            await hydrationCancellationTokenSource.CancelAsync();
            await renewalCancellationTokenSource.CancelAsync();

            try
            {
                await renewalTask;
            }
            catch (OperationCanceledException exception) when (renewalCancellationTokenSource.IsCancellationRequested)
            {
                logger.LogDebug(exception, "Organisation obligation historical backfill lease renewal stopped");
            }

            await leaseService.Release(CancellationToken.None);
        }
    }

    private async Task RenewLease(
        IOrganisationObligationHistoricalBackfillLeaseService leaseService,
        TimeSpan leaseDuration,
        CancellationTokenSource hydrationCancellationTokenSource,
        CancellationToken renewalCancellationToken
    )
    {
        using var renewalTimer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.LeaseRenewalIntervalSeconds));

        while (await renewalTimer.WaitForNextTickAsync(renewalCancellationToken))
        {
            try
            {
                if (await leaseService.TryRenew(leaseDuration, renewalCancellationToken))
                    continue;

                logger.LogError(
                    "Organisation obligation historical backfill stopped because its lease was not renewed"
                );
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Organisation obligation historical backfill lease renewal failed");
            }

            await hydrationCancellationTokenSource.CancelAsync();
            return;
        }
    }
}
