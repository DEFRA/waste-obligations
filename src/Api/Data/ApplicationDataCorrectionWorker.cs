using Defra.WasteObligations.Api.Services;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Data;

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
            if (!await WaitForMigrations(stoppingToken))
                return;

            var attempts = 0;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var duration = TimeSpan.FromSeconds(options.Value.LeaseDurationSeconds);
                    if (await TryAcquireLease(duration, stoppingToken))
                    {
                        if (await Run(duration, stoppingToken))
                            return;

                        attempts++;
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    attempts++;
                    logger.LogError(exception, "Application data corrections failed.");
                }
                if (attempts >= options.Value.MaximumAttempts)
                {
                    logger.LogError(
                        "Application data corrections did not complete after {AttemptCount} attempt(s). No further attempts will be made by this host.",
                        attempts
                    );

                    return;
                }
                await Task.Delay(TimeSpan.FromSeconds(options.Value.RetryDelaySeconds), timeProvider, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown while waiting for migrations or another correction lease.
        }
    }

    private async Task<bool> WaitForMigrations(CancellationToken stoppingToken)
    {
        try
        {
            await migrations.Wait(stoppingToken);

            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Application data corrections are blocked because Mongo migrations failed on this host."
            );

            return false;
        }
    }

    private async Task<bool> TryAcquireLease(TimeSpan duration, CancellationToken stoppingToken)
    {
        var acquiredLease = false;
        try
        {
            return await BackgroundOperationWatchdog.Run(
                async token =>
                {
                    acquiredLease = await lease.TryAcquire(duration, token);

                    return acquiredLease;
                },
                TimeSpan.FromSeconds(options.Value.LeaseOperationTimeoutSeconds),
                () =>
                {
                    if (stoppingToken.IsCancellationRequested)
                        logger.LogWarning(
                            "Application data correction lease acquisition cancellation is still draining."
                        );
                    else
                        logger.LogError(
                            "Application data correction lease acquisition stalled. Cancellation was requested; waiting for the call to stop."
                        );
                },
                stoppingToken,
                timeProvider,
                rejectLateResult: true
            );
        }
        catch (Exception exception)
        {
            if (acquiredLease)
                await Release();
            if (!stoppingToken.IsCancellationRequested)
                throw;
            if (exception is not OperationCanceledException)
                logger.LogWarning(
                    exception,
                    "Application data correction lease acquisition ended while cancellation was draining."
                );

            return false;
        }
    }

    private async Task<bool> Run(TimeSpan duration, CancellationToken stoppingToken)
    {
        using var work = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        using var renewal = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var renew = Renew(duration, work, renewal.Token);
        try
        {
            work.Token.ThrowIfCancellationRequested();
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IApplicationDataCorrectionRunner>().Run(work.Token);

            return true;
        }
        catch (OperationCanceledException exception)
            when (work.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation(
                exception,
                "Application data correction ownership was lost. Reacquiring a lease before another attempt."
            );

            return false;
        }
        finally
        {
            try
            {
                await BackgroundOperationCleanup.Run(work.CancelAsync, renewal.CancelAsync, () => renew, Release);
            }
            catch (AggregateException exception)
            {
                logger.LogError(
                    exception,
                    "Application data correction cleanup failed after draining; lease release was attempted."
                );
            }
        }
    }

    private async Task Release()
    {
        try
        {
            await BackgroundOperationWatchdog.Run(
                lease.Release,
                TimeSpan.FromSeconds(options.Value.LeaseOperationTimeoutSeconds),
                () =>
                    logger.LogWarning(
                        "Application data correction lease release stalled. Cancellation was requested; waiting for the call to stop."
                    ),
                CancellationToken.None,
                timeProvider
            );
        }
        catch (TimeoutException exception)
        {
            logger.LogWarning(
                exception,
                "Application data correction lease release timed out. It will expire automatically."
            );
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Application data correction lease could not be released. It will expire automatically."
            );
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
                if (
                    await BackgroundOperationWatchdog.Run(
                        token => lease.TryRenew(duration, token),
                        TimeSpan.FromSeconds(options.Value.LeaseOperationTimeoutSeconds),
                        async () =>
                        {
                            if (cancellationToken.IsCancellationRequested)
                                logger.LogWarning(
                                    "Application data correction lease renewal cancellation is still draining."
                                );
                            else
                                logger.LogError(
                                    "Application data correction lease renewal stalled. Cancelling correction work and waiting for the renewal call to stop."
                                );
                            await work.CancelAsync();
                        },
                        cancellationToken,
                        timeProvider
                    )
                )
                    continue;

                logger.LogError("Application data correction lease was lost; cancelling correction work.");
                await work.CancelAsync();

                return;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The owner stopped renewal after correction processing finished.
        }
        catch (TimeoutException) when (work.IsCancellationRequested)
        {
            // The watchdog already reported this stalled renewal and cancelled the work.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Application data correction lease renewal failed; cancelling correction work.");
            await work.CancelAsync();
        }
    }
}
