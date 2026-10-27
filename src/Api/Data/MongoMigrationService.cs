using Defra.WasteObligations.Api.Services;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Data;

public class MongoMigrationService(
    IMongoMigrationLeaseService leaseService,
    IMongoMigrationRunner migrationRunner,
    IOptions<MongoMigrationOptions> options,
    TimeProvider timeProvider,
    ILogger<MongoMigrationService> logger
) : BackgroundService
{
    private static readonly TimeSpan LeaseRetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LeaseReleaseTimeout = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting Mongo migrations.");

        try
        {
            var failedLeaseAcquisitions = 0;
            var leaseAcquisitionStartedAt = timeProvider.GetUtcNow();
            var leaseAcquisitionAlertLogged = false;

            while (!stoppingToken.IsCancellationRequested)
            {
                var leaseDuration = TimeSpan.FromSeconds(options.Value.LeaseDurationSeconds);
                bool acquired;

                try
                {
                    acquired = await BackgroundOperationWatchdog.Run(
                        token => leaseService.TryAcquire(leaseDuration, token),
                        TimeSpan.FromSeconds(options.Value.LeaseOperationTimeoutSeconds),
                        () =>
                            logger.LogError(
                                "Mongo migration lease acquisition stalled. Cancellation was requested; waiting for the call to stop."
                            ),
                        stoppingToken,
                        timeProvider,
                        rejectLateResult: true
                    );
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    if (failedLeaseAcquisitions == 0)
                    {
                        logger.LogError(exception, "Mongo migration lease acquisition failed. Retrying.");
                    }
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

                await RunMigrationsWithLease(leaseDuration, stoppingToken);

                return;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
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
        if (!leaseAcquisitionAlertLogged)
            leaseAcquisitionAlertLogged = LogLeaseAcquisitionAlertIfRequired(leaseAcquisitionStartedAt);

        await Task.Delay(LeaseRetryDelay, timeProvider, stoppingToken);

        return leaseAcquisitionAlertLogged;
    }

    private async Task RunMigrationsWithLease(TimeSpan leaseDuration, CancellationToken stoppingToken)
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
            var maximumAttempts = options.Value.MaximumAttempts;

            for (var attempt = 1; attempt <= maximumAttempts; attempt++)
            {
                if (await RunMigrationAttempt(attempt, migrationCancellationTokenSource.Token, stoppingToken))
                {
                    return;
                }

                if (migrationCancellationTokenSource.IsCancellationRequested)
                    return;

                if (attempt < maximumAttempts)
                {
                    logger.LogWarning(
                        "Mongo migration attempt {Attempt} did not complete. Retrying in {RetryDelay} while retaining the lease.",
                        attempt,
                        TimeSpan.FromSeconds(options.Value.RetryDelaySeconds)
                    );
                    try
                    {
                        await Task.Delay(
                            TimeSpan.FromSeconds(options.Value.RetryDelaySeconds),
                            migrationCancellationTokenSource.Token
                        );
                    }
                    catch (OperationCanceledException) when (migrationCancellationTokenSource.IsCancellationRequested)
                    {
                        return;
                    }
                }
            }

            logger.LogError(
                "Mongo migrations did not complete after {AttemptCount} attempt(s). No further attempts will be made by this host.",
                maximumAttempts
            );
        }
        finally
        {
            await migrationCancellationTokenSource.CancelAsync();
            await renewalCancellationTokenSource.CancelAsync();

            await renewalTask;

            await ReleaseLease();
        }
    }

    private async Task<bool> RunMigrationAttempt(
        int attempt,
        CancellationToken migrationCancellationToken,
        CancellationToken stoppingToken
    )
    {
        var timedOut = false;
        try
        {
            await BackgroundOperationWatchdog.Run(
                migrationRunner.Run,
                TimeSpan.FromSeconds(options.Value.AttemptTimeoutSeconds),
                () =>
                {
                    timedOut = true;
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
            if (!timedOut)
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
                        () =>
                        {
                            logger.LogError(
                                "Mongo migration lease renewal stalled. Cancelling migration work and waiting for the renewal call to stop."
                            );
                            migrationCancellationTokenSource.Cancel();
                        },
                        renewalCancellationToken,
                        timeProvider
                    )
                )
                    continue;

                logger.LogError("Mongo migration lease was not renewed. Cancelling the migration engine.");

                await migrationCancellationTokenSource.CancelAsync();
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
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Mongo migration lease could not be released. It will expire automatically.");
        }
        catch (OperationCanceledException exception)
        {
            logger.LogWarning(exception, "Mongo migration lease release timed out. It will expire automatically.");
        }
    }
}
