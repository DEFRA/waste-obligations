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
            try
            {
                await Hydrate(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Organisation obligation hydration failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds), stoppingToken);
        }
    }

    private async Task Hydrate(CancellationToken stoppingToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var leaseService = scope.ServiceProvider.GetRequiredService<IOrganisationObligationHydrationLeaseService>();
        var hydrationService = scope.ServiceProvider.GetRequiredService<IOrganisationObligationHydrationService>();
        var currentComplianceYearProvider = scope.ServiceProvider.GetRequiredService<ICurrentComplianceYearProvider>();
        var leaseDuration = TimeSpan.FromSeconds(options.Value.LeaseDurationSeconds);

        if (!await leaseService.TryAcquire(leaseDuration, stoppingToken))
        {
            logger.LogInformation("Organisation obligation hydration skipped because another instance holds the lease");
            return;
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
            var obligationYear = currentComplianceYearProvider.GetCurrentComplianceYear();
            var hydratedCount = await hydrationService.HydrateDue(
                obligationYear,
                hydrationCancellationTokenSource.Token
            );
            logger.LogInformation(
                "Organisation obligation hydration processed {HydratedCount} work items for obligation year {ObligationYear}",
                hydratedCount,
                obligationYear
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
