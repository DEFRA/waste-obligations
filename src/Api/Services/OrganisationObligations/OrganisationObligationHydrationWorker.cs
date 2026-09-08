using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Utils.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Services.OrganisationObligations;

public class OrganisationObligationHydrationWorker(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<OrganisationObligationHydrationOptions> options,
    IOrganisationObligationRequestPacer requestPacer,
    IOrganisationObligationHydrationMetrics metrics,
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
            metrics.LeaseNotAcquired();
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
            var currentWork = await hydrationService.PrepareDueWork(handover.CurrentObligationYear, cancellationToken);
            var outgoingWork = await hydrationService.PrepareDueWork(outgoingObligationYear, cancellationToken);

            return await HydrateOverlappingYears(
                hydrationService,
                currentWork,
                outgoingWork,
                deactivateSecondaryAfterSuccessfulRead: true,
                cancellationToken: cancellationToken
            );
        }

        if (handover.IncomingObligationYear is { } incomingObligationYear)
        {
            var currentWork = await hydrationService.PrepareDueWork(handover.CurrentObligationYear, cancellationToken);
            var incomingWork = await hydrationService.PrepareDueWork(incomingObligationYear, cancellationToken);

            return await HydrateOverlappingYears(
                hydrationService,
                currentWork,
                incomingWork,
                deactivateSecondaryAfterSuccessfulRead: false,
                cancellationToken: cancellationToken
            );
        }

        return await hydrationService.HydrateDue(
            handover.CurrentObligationYear,
            cancellationToken,
            maximumWork: options.Value.BatchSize
        );
    }

    private async Task<int> HydrateOverlappingYears(
        IOrganisationObligationHydrationService hydrationService,
        OrganisationObligationHydrationPreparedWork currentWork,
        OrganisationObligationHydrationPreparedWork secondaryWork,
        bool deactivateSecondaryAfterSuccessfulRead,
        CancellationToken cancellationToken
    )
    {
        var totalActiveSummaryCount = currentWork.ActiveSummaryCount + secondaryWork.ActiveSummaryCount;
        await requestPacer.ObserveWorkload(totalActiveSummaryCount, cancellationToken);
        var pacing = await requestPacer.GetStatus(cancellationToken);
        metrics.QueueObserved(totalActiveSummaryCount, currentWork.DueSummaryCount + secondaryWork.DueSummaryCount);
        metrics.CapacityObserved(
            totalActiveSummaryCount,
            options.Value.MaxDownstreamRequestsPerMinute,
            pacing.DesiredRequestsPerMinute,
            pacing.EffectiveRequestsPerMinute,
            options.Value.RefreshInterval
        );
        var currentMaximumWork = CurrentYearMaximumWork(currentWork, secondaryWork);
        var currentHydratedCount =
            currentMaximumWork == 0
                ? 0
                : await hydrationService.HydratePreparedDueWork(
                    currentWork,
                    cancellationToken,
                    currentMaximumWork,
                    recordWorkloadMetrics: false
                );
        var secondaryMaximumWork = options.Value.BatchSize - currentHydratedCount;
        var secondaryHydratedCount =
            secondaryMaximumWork == 0
                ? 0
                : await hydrationService.HydratePreparedDueWork(
                    secondaryWork,
                    cancellationToken,
                    secondaryMaximumWork,
                    deactivateSecondaryAfterSuccessfulRead,
                    recordWorkloadMetrics: false
                );

        return currentHydratedCount + secondaryHydratedCount;
    }

    private int CurrentYearMaximumWork(
        OrganisationObligationHydrationPreparedWork currentWork,
        OrganisationObligationHydrationPreparedWork secondaryWork
    )
    {
        if (currentWork.ActiveSummaryCount == 0)
            return 0;

        if (secondaryWork.ActiveSummaryCount == 0)
            return options.Value.BatchSize;

        var totalActiveSummaryCount = currentWork.ActiveSummaryCount + secondaryWork.ActiveSummaryCount;
        var proportionalCurrentWork = (int)
            Math.Floor(options.Value.BatchSize * currentWork.ActiveSummaryCount / (double)totalActiveSummaryCount);

        return Math.Clamp(proportionalCurrentWork, 1, options.Value.BatchSize - 1);
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
