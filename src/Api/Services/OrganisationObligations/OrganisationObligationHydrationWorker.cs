using Defra.WasteObligations.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Services.OrganisationObligations;

public class OrganisationObligationHydrationWorker(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<OrganisationObligationHydrationOptions> options,
    ILogger<OrganisationObligationHydrationWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.PollingEnabled)
        {
            logger.LogInformation("Organisation obligation hydration polling is off");
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var hydratedCount = 0;

            try
            {
                hydratedCount = await Hydrate(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Organisation obligation hydration failed");
            }

            if (hydratedCount >= options.Value.BatchSize)
                continue;

            await Task.Delay(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds), stoppingToken);
        }
    }

    private async Task<int> Hydrate(CancellationToken stoppingToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var leaseService = scope.ServiceProvider.GetRequiredService<IOrganisationObligationHydrationLeaseService>();
        var hydrationService = scope.ServiceProvider.GetRequiredService<IOrganisationObligationHydrationService>();
        var currentObligationYearProvider = scope.ServiceProvider.GetRequiredService<ICurrentObligationYearProvider>();
        var leaseDuration = TimeSpan.FromSeconds(options.Value.LeaseDurationSeconds);

        if (!await leaseService.TryAcquire(leaseDuration, stoppingToken))
        {
            logger.LogInformation("Organisation obligation hydration skipped because another instance holds the lease");
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

        var hydratedCount = 0;

        try
        {
            var handover = currentObligationYearProvider.GetHandover(options.Value.OutgoingYearGracePeriod);
            hydratedCount = await Hydrate(hydrationService, handover, hydrationCancellationTokenSource.Token);
            logger.LogInformation(
                "Organisation obligation hydration processed {HydratedCount} work items for obligation year {ObligationYear}",
                hydratedCount,
                handover.CurrentObligationYear
            );
        }
        catch (OperationCanceledException exception)
            when (hydrationCancellationTokenSource.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Organisation obligation hydration stopped because its lease was not renewed");
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
                logger.LogDebug(exception, "Organisation obligation hydration lease renewal stopped");
            }

            await leaseService.Release(CancellationToken.None);
        }

        return hydratedCount;
    }

    private async Task<int> Hydrate(
        IOrganisationObligationHydrationService hydrationService,
        ObligationYearHandover handover,
        CancellationToken cancellationToken
    )
    {
        if (
            handover.OutgoingObligationYear is { } outgoingObligationYear
            && handover.OutgoingYearCutoverAt is { } outgoingYearCutoverAt
        )
        {
            await hydrationService.EnqueueReconciliation(
                outgoingObligationYear,
                outgoingYearCutoverAt,
                cancellationToken
            );
            var currentYearHydratedCount = await hydrationService.HydrateDue(
                handover.CurrentObligationYear,
                cancellationToken,
                maximumWork: options.Value.BatchSize - 1
            );
            var outgoingHydratedCount = await hydrationService.HydrateDue(
                outgoingObligationYear,
                cancellationToken,
                maximumWork: options.Value.BatchSize - currentYearHydratedCount
            );

            return outgoingHydratedCount + currentYearHydratedCount;
        }

        if (handover.IncomingObligationYear is not { } incomingObligationYear)
        {
            return await hydrationService.HydrateDue(
                handover.CurrentObligationYear,
                cancellationToken,
                maximumWork: options.Value.BatchSize
            );
        }

        var incomingHydratedCount = await hydrationService.HydrateDue(
            incomingObligationYear,
            cancellationToken,
            maximumWork: 1
        );
        var currentHydratedCount = await hydrationService.HydrateDue(
            handover.CurrentObligationYear,
            cancellationToken,
            maximumWork: options.Value.BatchSize - incomingHydratedCount
        );

        return currentHydratedCount + incomingHydratedCount;
    }

    private async Task RenewLease(
        IOrganisationObligationHydrationLeaseService leaseService,
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

                logger.LogError("Organisation obligation hydration stopped because its lease was not renewed");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Organisation obligation hydration lease renewal failed");
            }

            await hydrationCancellationTokenSource.CancelAsync();
            return;
        }
    }
}
