using Defra.WasteObligations.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Services.OrganisationEligibility;

public class OrganisationEligibilityRefreshWorker(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<OrganisationEligibilityOptions> options,
    ILogger<OrganisationEligibilityRefreshWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.RefreshPollingEnabled)
        {
            logger.LogInformation("Organisation eligibility refresh polling is off");
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Refresh(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Organisation eligibility refresh failed");
            }

            await Delay(stoppingToken);
        }
    }

    private async Task Refresh(CancellationToken stoppingToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var leaseService = scope.ServiceProvider.GetRequiredService<IOrganisationEligibilityRefreshLeaseService>();
        var refreshService = scope.ServiceProvider.GetRequiredService<IOrganisationEligibilityRefreshService>();
        var reviewStateBackfillService =
            scope.ServiceProvider.GetRequiredService<IComplianceDeclarationReviewStateBackfillService>();
        var leaseDuration = TimeSpan.FromSeconds(options.Value.RefreshLeaseDurationSeconds);

        if (!await leaseService.TryAcquire(leaseDuration, stoppingToken))
        {
            logger.LogInformation("Organisation eligibility refresh skipped because another instance holds the lease");
            return;
        }

        using var refreshCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        using var renewalCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var renewalTask = RenewLease(
            leaseService,
            leaseDuration,
            refreshCancellationTokenSource,
            renewalCancellationTokenSource.Token
        );

        try
        {
            await BackfillReviewState(reviewStateBackfillService, refreshCancellationTokenSource.Token);
            var result = await refreshService.Refresh(refreshCancellationTokenSource.Token);
            logger.LogInformation(
                "Organisation eligibility refresh {Outcome} with {RowCount} rows",
                result.Outcome,
                result.RowCount
            );
        }
        catch (OperationCanceledException exception)
            when (refreshCancellationTokenSource.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Organisation eligibility refresh stopped because its lease was not renewed");
        }
        finally
        {
            await refreshCancellationTokenSource.CancelAsync();
            await renewalCancellationTokenSource.CancelAsync();

            try
            {
                await renewalTask;
            }
            catch (OperationCanceledException exception) when (renewalCancellationTokenSource.IsCancellationRequested)
            {
                logger.LogDebug(exception, "Organisation eligibility refresh lease renewal stopped");
            }

            await leaseService.Release(CancellationToken.None);
        }
    }

    private async Task RenewLease(
        IOrganisationEligibilityRefreshLeaseService leaseService,
        TimeSpan leaseDuration,
        CancellationTokenSource refreshCancellationTokenSource,
        CancellationToken renewalCancellationToken
    )
    {
        using var renewalTimer = new PeriodicTimer(
            TimeSpan.FromSeconds(options.Value.RefreshLeaseRenewalIntervalSeconds)
        );

        while (await renewalTimer.WaitForNextTickAsync(renewalCancellationToken))
        {
            try
            {
                if (await leaseService.TryRenew(leaseDuration, renewalCancellationToken))
                    continue;

                logger.LogError("Organisation eligibility refresh stopped because its lease was not renewed");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Organisation eligibility refresh lease renewal failed");
            }

            await refreshCancellationTokenSource.CancelAsync();
            return;
        }
    }

    private async Task BackfillReviewState(
        IComplianceDeclarationReviewStateBackfillService reviewStateBackfillService,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var result = await reviewStateBackfillService.Backfill(cancellationToken);
            if (!result.AlreadyComplete)
            {
                logger.LogInformation(
                    "Compliance declaration review state backfill completed with {StateRowCount} rows",
                    result.StateRowCount
                );
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Compliance declaration review state backfill failed");
        }
    }

    private Task Delay(CancellationToken cancellationToken) =>
        Task.Delay(TimeSpan.FromSeconds(options.Value.RefreshPollIntervalSeconds), cancellationToken);
}
