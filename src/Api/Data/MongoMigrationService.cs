using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Data;

[ExcludeFromCodeCoverage(Justification = "See integration tests")]
public class MongoMigrationService(
    IMongoMigrationLeaseService leaseService,
    IMongoMigrationRunner migrationRunner,
    IOptions<MongoMigrationOptions> options,
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
            var waitingForLease = false;
            var failedLeaseAcquisitions = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                var leaseDuration = TimeSpan.FromSeconds(options.Value.LeaseDurationSeconds);
                bool acquired;

                try
                {
                    acquired = await leaseService.TryAcquire(leaseDuration, stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    if (failedLeaseAcquisitions == 0)
                    {
                        logger.LogError(exception, "Mongo migration lease acquisition failed. Retrying.");
                    }
                    else
                    {
                        logger.LogDebug(exception, "Mongo migration lease acquisition failed again. Retrying.");
                    }

                    failedLeaseAcquisitions++;
                    await Task.Delay(LeaseRetryDelay, stoppingToken);
                    continue;
                }

                if (!acquired)
                {
                    if (!waitingForLease)
                    {
                        logger.LogInformation(
                            "Mongo migration lease is held by another host. Waiting before retrying."
                        );
                        waitingForLease = true;
                    }

                    await Task.Delay(LeaseRetryDelay, stoppingToken);
                    continue;
                }

                if (failedLeaseAcquisitions > 0)
                {
                    logger.LogInformation(
                        "Mongo migration lease acquisition recovered after {FailureCount} failure(s).",
                        failedLeaseAcquisitions
                    );
                }

                logger.LogInformation("Mongo migration lease acquired by {InstanceId}.", leaseService.InstanceId);
                await RunMigrationsWithLease(leaseDuration, stoppingToken);

                return;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
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
                    return;

                if (migrationCancellationTokenSource.IsCancellationRequested)
                    return;

                if (attempt == maximumAttempts)
                {
                    logger.LogError(
                        "Mongo migrations did not complete after {AttemptCount} attempt(s). No further attempts will be made by this host.",
                        maximumAttempts
                    );
                    return;
                }

                logger.LogWarning(
                    "Mongo migration attempt {Attempt} did not complete. Retrying in {RetryDelay} while retaining the lease.",
                    attempt,
                    TimeSpan.FromSeconds(options.Value.RetryDelaySeconds)
                );
                await Task.Delay(
                    TimeSpan.FromSeconds(options.Value.RetryDelaySeconds),
                    migrationCancellationTokenSource.Token
                );
            }
        }
        catch (OperationCanceledException exception) when (migrationCancellationTokenSource.IsCancellationRequested)
        {
            logger.LogDebug(exception, "Mongo migration operation stopped.");
        }
        finally
        {
            await migrationCancellationTokenSource.CancelAsync();
            await renewalCancellationTokenSource.CancelAsync();

            try
            {
                await renewalTask;
            }
            catch (OperationCanceledException exception) when (renewalCancellationTokenSource.IsCancellationRequested)
            {
                logger.LogDebug(exception, "Mongo migration lease renewal stopped.");
            }

            await ReleaseLease();
        }
    }

    private async Task<bool> RunMigrationAttempt(
        int attempt,
        CancellationToken migrationCancellationToken,
        CancellationToken stoppingToken
    )
    {
        using var attemptCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            migrationCancellationToken
        );
        var migrationTask = migrationRunner.Run(attemptCancellationTokenSource.Token);
        var timeout = TimeSpan.FromSeconds(options.Value.AttemptTimeoutSeconds);
        var completedTask = await Task.WhenAny(migrationTask, Task.Delay(timeout, stoppingToken));

        if (completedTask != migrationTask)
        {
            if (stoppingToken.IsCancellationRequested || migrationCancellationToken.IsCancellationRequested)
                return await WaitForMigrationAttempt(
                    migrationTask,
                    attempt,
                    false,
                    attemptCancellationTokenSource.Token
                );

            logger.LogError(
                "Mongo migration attempt {Attempt} exceeded its {AttemptTimeout} limit. Cancellation was requested; the exclusive lease will be retained until the migration engine stops.",
                attempt,
                timeout
            );
            await attemptCancellationTokenSource.CancelAsync();

            return await WaitForMigrationAttempt(migrationTask, attempt, true, attemptCancellationTokenSource.Token);
        }

        return await WaitForMigrationAttempt(migrationTask, attempt, false, attemptCancellationTokenSource.Token);
    }

    private async Task<bool> WaitForMigrationAttempt(
        Task migrationTask,
        int attempt,
        bool timeoutLogged,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await migrationTask;

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            if (!timeoutLogged)
            {
                logger.LogError(exception, "Mongo migration attempt {Attempt} failed.", attempt);
            }

            return false;
        }
    }

    private async Task RenewLease(
        TimeSpan leaseDuration,
        CancellationTokenSource migrationCancellationTokenSource,
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

                logger.LogError("Mongo migration lease was not renewed. Cancelling the migration engine.");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Mongo migration lease renewal failed. Cancelling the migration engine.");
            }

            await migrationCancellationTokenSource.CancelAsync();
            return;
        }
    }

    private async Task ReleaseLease()
    {
        using var releaseCancellationTokenSource = new CancellationTokenSource(LeaseReleaseTimeout);

        try
        {
            await leaseService.Release(releaseCancellationTokenSource.Token);
            logger.LogInformation("Mongo migration lease released by {InstanceId}.", leaseService.InstanceId);
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
