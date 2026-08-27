using Defra.WasteObligations.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Services.OrganisationEligibility;

// TEMPORARY INITIAL ROLLOUT: Remove this worker, its option, service method and persisted completion marker
// once InitialRolloutReconciliationCompletedAt is populated in every deployed environment.
public class ComplianceDeclarationReviewStateInitialRolloutReconciliationWorker(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<OrganisationEligibilityOptions> options,
    ILogger<ComplianceDeclarationReviewStateInitialRolloutReconciliationWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(options.Value.InitialRolloutReconciliationDelaySeconds), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await TryReconcile(stoppingToken))
                    return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Compliance declaration review state initial rollout reconciliation failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task<bool> TryReconcile(CancellationToken stoppingToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var leaseService = scope.ServiceProvider.GetRequiredService<IOrganisationEligibilityRefreshLeaseService>();
        var reviewStateBackfillService =
            scope.ServiceProvider.GetRequiredService<IComplianceDeclarationReviewStateBackfillService>();
        var leaseDuration = TimeSpan.FromSeconds(options.Value.RefreshLeaseDurationSeconds);

        if (!await leaseService.TryAcquire(leaseDuration, stoppingToken))
        {
            logger.LogInformation(
                "Compliance declaration review state initial rollout reconciliation skipped because another instance holds the lease"
            );

            return false;
        }

        using var reconciliationCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            stoppingToken
        );
        using var renewalCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var renewalTask = RenewLease(
            leaseService,
            leaseDuration,
            reconciliationCancellationTokenSource,
            renewalCancellationTokenSource.Token
        );

        try
        {
            var result = await reviewStateBackfillService.ReconcileInitialRollout(
                reconciliationCancellationTokenSource.Token
            );
            if (!result.AlreadyComplete)
            {
                logger.LogInformation(
                    "Compliance declaration review state initial rollout reconciliation completed with {StateRowCount} rows",
                    result.StateRowCount
                );
            }

            return true;
        }
        catch (OperationCanceledException exception)
            when (reconciliationCancellationTokenSource.IsCancellationRequested
                && !stoppingToken.IsCancellationRequested
            )
        {
            logger.LogWarning(
                exception,
                "Compliance declaration review state initial rollout reconciliation stopped because its lease was not renewed"
            );

            return false;
        }
        finally
        {
            await reconciliationCancellationTokenSource.CancelAsync();
            await renewalCancellationTokenSource.CancelAsync();

            try
            {
                await renewalTask;
            }
            catch (OperationCanceledException exception) when (renewalCancellationTokenSource.IsCancellationRequested)
            {
                logger.LogDebug(exception, "Initial rollout reconciliation lease renewal stopped");
            }

            await leaseService.Release(CancellationToken.None);
        }
    }

    private async Task RenewLease(
        IOrganisationEligibilityRefreshLeaseService leaseService,
        TimeSpan leaseDuration,
        CancellationTokenSource reconciliationCancellationTokenSource,
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

                logger.LogError(
                    "Compliance declaration review state initial rollout reconciliation stopped because its lease was not renewed"
                );
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Initial rollout reconciliation lease renewal failed");
            }

            await reconciliationCancellationTokenSource.CancelAsync();
            return;
        }
    }
}
