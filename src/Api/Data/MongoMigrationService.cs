using Defra.WasteObligations.Api.Services;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Data;

public class MongoMigrationService(
    IMongoMigrationLeaseService leaseService,
    IMongoMigrationRunner migrationRunner,
    IOptions<MongoMigrationOptions> options,
    TimeProvider timeProvider,
    ILogger<MongoMigrationService> logger,
    MongoMigrationCompletion completion
) : BackgroundService
{
    private static readonly TimeSpan LeaseRetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LeaseReleaseTimeout = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting Mongo migrations.");
        var attempts = 0;
        try
        {
            var failedLeaseAcquisitions = 0;
            var leaseAcquisitionStartedAt = timeProvider.GetUtcNow();
            var leaseAcquisitionAlertLogged = false;
            while (attempts < options.Value.MaximumAttempts)
            {
                stoppingToken.ThrowIfCancellationRequested();
                var leaseDuration = TimeSpan.FromSeconds(options.Value.LeaseDurationSeconds);
                bool acquired;
                try
                {
                    acquired = await TryAcquireLease(leaseDuration, stoppingToken);
                }
                catch (Exception exception)
                {
                    if (!CanRetryAcquisition(exception, failedLeaseAcquisitions, stoppingToken))
                        return;
                    failedLeaseAcquisitions++;
                    leaseAcquisitionAlertLogged = await WaitForLeaseRetry(
                        leaseAcquisitionStartedAt,
                        leaseAcquisitionAlertLogged,
                        stoppingToken
                    );
                    continue;
                }
                if (!acquired)
                {
                    leaseAcquisitionAlertLogged = await WaitForLeaseRetry(
                        leaseAcquisitionStartedAt,
                        leaseAcquisitionAlertLogged,
                        stoppingToken
                    );
                    continue;
                }

                var result = await RunMigrationsWithLease(leaseDuration, attempts, stoppingToken);
                attempts = result.Attempts;
                if (result.Completed)
                    return;
                stoppingToken.ThrowIfCancellationRequested();
                if (attempts < options.Value.MaximumAttempts)
                {
                    failedLeaseAcquisitions = 0;
                    leaseAcquisitionStartedAt = timeProvider.GetUtcNow();
                    leaseAcquisitionAlertLogged = false;
                    logger.LogWarning(
                        "Mongo migration lease processing stopped. Reacquiring ownership before another attempt."
                    );
                    await Task.Delay(
                        TimeSpan.FromSeconds(options.Value.RetryDelaySeconds),
                        timeProvider,
                        stoppingToken
                    );
                }
            }
            logger.LogError(
                "Mongo migrations did not complete after {AttemptCount} attempt(s). No further attempts will be made by this host.",
                options.Value.MaximumAttempts
            );
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown while waiting for ownership or another attempt.
        }
        finally
        {
            if (!stoppingToken.IsCancellationRequested)
                completion.Fail(new InvalidOperationException("Mongo migrations did not complete on this host."));
        }
    }

    private async Task<bool> TryAcquireLease(TimeSpan leaseDuration, CancellationToken stoppingToken)
    {
        var acquiredLease = false;
        try
        {
            return await BackgroundOperationWatchdog.Run(
                async token =>
                {
                    acquiredLease = await leaseService.TryAcquire(leaseDuration, token);

                    return acquiredLease;
                },
                TimeSpan.FromSeconds(options.Value.LeaseOperationTimeoutSeconds),
                () =>
                {
                    if (stoppingToken.IsCancellationRequested)
                        logger.LogWarning("Mongo migration lease acquisition cancellation is still draining.");
                    else
                        logger.LogError(
                            "Mongo migration lease acquisition stalled. Cancellation was requested; waiting for the call to stop."
                        );
                },
                stoppingToken,
                timeProvider,
                rejectLateResult: true
            );
        }
        catch
        {
            // The watchdog has drained acquisition before cleanup can release ownership.
            if (acquiredLease)
                await ReleaseLease();

            throw;
        }
    }

    private bool CanRetryAcquisition(Exception exception, int failedLeaseAcquisitions, CancellationToken stoppingToken)
    {
        if (stoppingToken.IsCancellationRequested)
        {
            if (exception is not OperationCanceledException and not TimeoutException)
                logger.LogError(exception, "Mongo migration lease acquisition failed while the host was stopping.");

            return false;
        }
        if (failedLeaseAcquisitions == 0)
            logger.LogError(exception, "Mongo migration lease acquisition failed. Retrying.");

        return true;
    }

    private bool LogLeaseAcquisitionAlertIfRequired(DateTimeOffset leaseAcquisitionStartedAt)
    {
        var leaseWaitDuration = timeProvider.GetUtcNow() - leaseAcquisitionStartedAt;
        var alertThreshold = TimeSpan.FromSeconds(options.Value.LeaseAcquisitionAlertThresholdSeconds);

        if (leaseWaitDuration < alertThreshold)
            return false;

        logger.LogError(
            "Mongo migration lease has not been acquired after {LeaseWaitDuration}. Retrying while the API remains healthy.",
            leaseWaitDuration
        );

        return true;
    }

    private async Task<bool> WaitForLeaseRetry(
        DateTimeOffset leaseAcquisitionStartedAt,
        bool leaseAcquisitionAlertLogged,
        CancellationToken stoppingToken
    )
    {
        stoppingToken.ThrowIfCancellationRequested();
        if (!leaseAcquisitionAlertLogged)
            leaseAcquisitionAlertLogged = LogLeaseAcquisitionAlertIfRequired(leaseAcquisitionStartedAt);

        await Task.Delay(LeaseRetryDelay, timeProvider, stoppingToken);

        return leaseAcquisitionAlertLogged;
    }

    private async Task<(bool Completed, int Attempts)> RunMigrationsWithLease(
        TimeSpan leaseDuration,
        int attempts,
        CancellationToken stoppingToken
    )
    {
        using var migrationCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        using var renewalCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var renewalTask = RenewLease(
            leaseDuration,
            migrationCancellationTokenSource,
            renewalCancellationTokenSource.Token
        );
        try
        {
            while (attempts < options.Value.MaximumAttempts)
            {
                attempts++;
                if (await RunMigrationAttempt(attempts, migrationCancellationTokenSource.Token, stoppingToken))
                {
                    completion.Complete();

                    return (true, attempts);
                }
                if (migrationCancellationTokenSource.IsCancellationRequested)
                    return (false, attempts);
                if (attempts < options.Value.MaximumAttempts)
                {
                    logger.LogWarning(
                        "Mongo migration attempt {Attempt} did not complete. Retrying in {RetryDelay} while retaining the lease.",
                        attempts,
                        TimeSpan.FromSeconds(options.Value.RetryDelaySeconds)
                    );
                    try
                    {
                        await Task.Delay(
                            TimeSpan.FromSeconds(options.Value.RetryDelaySeconds),
                            timeProvider,
                            migrationCancellationTokenSource.Token
                        );
                    }
                    catch (OperationCanceledException) when (migrationCancellationTokenSource.IsCancellationRequested)
                    {
                        return (false, attempts);
                    }
                }
            }

            return (false, attempts);
        }
        finally
        {
            try
            {
                await BackgroundOperationCleanup.Run(
                    migrationCancellationTokenSource.CancelAsync,
                    renewalCancellationTokenSource.CancelAsync,
                    () => renewalTask,
                    ReleaseLease
                );
            }
            catch (AggregateException exception)
            {
                logger.LogError(
                    exception,
                    "Mongo migration cleanup failed after draining; lease release was attempted."
                );
            }
        }
    }

    private async Task<bool> RunMigrationAttempt(
        int attempt,
        CancellationToken migrationCancellationToken,
        CancellationToken stoppingToken
    )
    {
        try
        {
            await BackgroundOperationWatchdog.Run(
                migrationRunner.Run,
                TimeSpan.FromSeconds(options.Value.AttemptTimeoutSeconds),
                () =>
                {
                    if (stoppingToken.IsCancellationRequested || migrationCancellationToken.IsCancellationRequested)
                        logger.LogWarning(
                            "Mongo migration attempt {Attempt} cancellation is still draining after {AttemptTimeout}.",
                            attempt,
                            TimeSpan.FromSeconds(options.Value.AttemptTimeoutSeconds)
                        );
                    else
                        logger.LogError(
                            "Mongo migration attempt {Attempt} exceeded its {AttemptTimeout} limit. Cancellation was requested; the exclusive lease will be retained until the migration engine stops.",
                            attempt,
                            TimeSpan.FromSeconds(options.Value.AttemptTimeoutSeconds)
                        );
                },
                migrationCancellationToken,
                timeProvider
            );

            return true;
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested || migrationCancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Mongo migration attempt {Attempt} failed.", attempt);

            return false;
        }
    }

    private async Task RenewLease(
        TimeSpan leaseDuration,
        CancellationTokenSource migrationCancellationTokenSource,
        CancellationToken renewalCancellationToken
    )
    {
        using var renewalTimer = new PeriodicTimer(
            TimeSpan.FromSeconds(options.Value.LeaseRenewalIntervalSeconds),
            timeProvider
        );

        try
        {
            while (await renewalTimer.WaitForNextTickAsync(renewalCancellationToken))
            {
                if (
                    await BackgroundOperationWatchdog.Run(
                        token => leaseService.TryRenew(leaseDuration, token),
                        TimeSpan.FromSeconds(options.Value.LeaseOperationTimeoutSeconds),
                        async () =>
                        {
                            if (renewalCancellationToken.IsCancellationRequested)
                                logger.LogWarning("Mongo migration lease renewal cancellation is still draining.");
                            else
                                logger.LogError(
                                    "Mongo migration lease renewal stalled. Cancelling migration work and waiting for the renewal call to stop."
                                );
                            await migrationCancellationTokenSource.CancelAsync();
                        },
                        renewalCancellationToken,
                        timeProvider
                    )
                )
                    continue;

                logger.LogError("Mongo migration lease was not renewed. Cancelling the migration engine.");

                await migrationCancellationTokenSource.CancelAsync();

                return;
            }
        }
        catch (OperationCanceledException) when (renewalCancellationToken.IsCancellationRequested)
        {
            // Expected when migration processing stops.
        }
        catch (TimeoutException) when (migrationCancellationTokenSource.IsCancellationRequested)
        {
            // The watchdog already reported this stalled renewal and cancelled the work.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Mongo migration lease renewal failed. Cancelling the migration engine.");

            await migrationCancellationTokenSource.CancelAsync();
        }
    }

    private async Task ReleaseLease()
    {
        try
        {
            await BackgroundOperationWatchdog.Run(
                leaseService.Release,
                LeaseReleaseTimeout,
                () =>
                    logger.LogWarning(
                        "Mongo migration lease release stalled. Cancellation was requested; waiting for the call to stop."
                    ),
                CancellationToken.None,
                timeProvider
            );
        }
        catch (TimeoutException exception)
        {
            logger.LogWarning(exception, "Mongo migration lease release timed out. It will expire automatically.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Mongo migration lease could not be released. It will expire automatically.");
        }
    }
}
