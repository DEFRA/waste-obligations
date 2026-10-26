using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Data.Corrections;

public class ApplicationDataCorrectionWorker(
    MongoMigrationCompletion migrations,
    IServiceScopeFactory scopeFactory,
    IApplicationDataCorrectionLeaseService lease,
    IOptions<ApplicationDataCorrectionOptions> options,
    TimeProvider timeProvider,
    ILogger<ApplicationDataCorrectionWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await migrations.Wait(stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var duration = TimeSpan.FromSeconds(options.Value.LeaseDurationSeconds);
                    if (await lease.TryAcquire(duration, stoppingToken))
                    {
                        await Run(duration, stoppingToken);

                        return;
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Application data corrections failed; retrying independently of schema migrations."
                    );
                }
                await Task.Delay(TimeSpan.FromSeconds(options.Value.RetryDelaySeconds), timeProvider, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown while waiting for migrations or another correction lease.
        }
    }

    private async Task Run(TimeSpan duration, CancellationToken stoppingToken)
    {
        using var work = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        using var renewal = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var renew = Renew(duration, work, renewal.Token);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IApplicationDataCorrectionRunner>().Run(work.Token);
            work.Token.ThrowIfCancellationRequested();
        }
        finally
        {
            await renewal.CancelAsync();
            await renew;
            using var release = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await lease.Release(release.Token);
        }
    }

    private async Task Renew(TimeSpan duration, CancellationTokenSource work, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(options.Value.LeaseRenewalIntervalSeconds),
            timeProvider
        );
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (await lease.TryRenew(duration, cancellationToken))
                    continue;

                logger.LogError("Application data correction lease was lost; cancelling correction work.");
                await work.CancelAsync();

                return;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The owner stopped the renewal loop after correction processing finished.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Application data correction lease renewal failed; cancelling correction work.");
            await work.CancelAsync();
        }
    }
}
