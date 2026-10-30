using System.Runtime.ExceptionServices;
using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services.Scheduling;

public sealed class ScheduledJobWorker(
    string name,
    CronExpression schedule,
    Type jobType,
    IServiceScopeFactory scopeFactory,
    IMongoDatabase database,
    IScheduledJobLeaseService leaseService,
    TimeProvider timeProvider,
    ILogger<ScheduledJobWorker> logger
) : BackgroundService
{
    private const int DuplicateKeyErrorCode = 11000;
    private static readonly TimeSpan s_maximumWait = TimeSpan.FromDays(1);
    private static readonly TimeSpan s_leaseOperationTimeout = TimeSpan.FromSeconds(10);

    private readonly TimeSpan _leaseDuration = TimeSpan.FromMinutes(1);

    private readonly IMongoCollection<ScheduledJobState> _states = database.GetCollection<ScheduledJobState>(
        "_scheduled_job_state"
    );

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var next = schedule.GetNextOccurrence(timeProvider.GetUtcNow().UtcDateTime);
            if (next is not { } scheduledAt)
            {
                logger.LogWarning("Scheduled job {JobName} has no future cron occurrence", name);

                return;
            }

            try
            {
                var delay = scheduledAt - timeProvider.GetUtcNow().UtcDateTime;
                while (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay < s_maximumWait ? delay : s_maximumWait, timeProvider, stoppingToken);
                    delay = scheduledAt - timeProvider.GetUtcNow().UtcDateTime;
                }

                await Run(scheduledAt, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Scheduled job {JobName} failed for {ScheduledAt}", name, scheduledAt);
            }
        }
    }

    private async Task Run(DateTime scheduledAt, CancellationToken stoppingToken)
    {
        if (!await TryAcquireLease(stoppingToken))
            return;

        using var work = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        using var renewal = new CancellationTokenSource();
        var renewalTask = RenewLease(work, renewal.Token);
        Exception? processingFailure = null;
        AsyncServiceScope? scope = null;

        try
        {
            if (await TryStart(scheduledAt, work.Token))
            {
                work.Token.ThrowIfCancellationRequested();
                logger.LogInformation("Scheduled job {JobName} started for {ScheduledAt}", name, scheduledAt);
                scope = scopeFactory.CreateAsyncScope();
                var job = (IScheduledJob)scope.Value.ServiceProvider.GetRequiredService(jobType);
                await job.Run(work.Token);
                logger.LogInformation("Scheduled job {JobName} finished for {ScheduledAt}", name, scheduledAt);
            }
        }
        catch (Exception exception)
        {
            processingFailure = exception;
        }

        try
        {
            await BackgroundOperationCleanup.Run(
                () => scope is { } createdScope ? createdScope.DisposeAsync().AsTask() : Task.CompletedTask,
                work.CancelAsync,
                renewal.CancelAsync,
                () => renewalTask,
                ReleaseLease
            );
        }
        catch (Exception exception) when (processingFailure is not null)
        {
            throw new AggregateException("Scheduled job execution and cleanup failed.", processingFailure, exception);
        }

        if (processingFailure is not null)
            ExceptionDispatchInfo.Capture(processingFailure).Throw();
    }

    private async Task<bool> TryStart(DateTime scheduledAt, CancellationToken cancellationToken)
    {
        var filter = Builders<ScheduledJobState>.Filter.And(
            Builders<ScheduledJobState>.Filter.Eq(x => x.Id, name),
            Builders<ScheduledJobState>.Filter.Lt(x => x.LastStartedAt, scheduledAt)
        );
        var update = Builders<ScheduledJobState>
            .Update.SetOnInsert(x => x.Id, name)
            .Set(x => x.LastStartedAt, scheduledAt);

        try
        {
            await _states.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<ScheduledJobState> { IsUpsert = true },
                cancellationToken
            );

            return true;
        }
        catch (MongoCommandException exception) when (exception.Code == DuplicateKeyErrorCode)
        {
            return false;
        }
    }

    private async Task RenewLease(CancellationTokenSource work, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20), timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (
                    await BackgroundOperationWatchdog.Run(
                        token => leaseService.TryRenew(_leaseDuration, token),
                        s_leaseOperationTimeout,
                        async () =>
                        {
                            logger.LogError(
                                "Scheduled job {JobName} lease renewal stalled; cancelling work and draining the call",
                                name
                            );
                            await work.CancelAsync();
                        },
                        cancellationToken,
                        timeProvider,
                        rejectLateResult: true
                    )
                )
                    continue;

                logger.LogError("Scheduled job {JobName} lost its lease", name);
                await work.CancelAsync();

                return;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Renewal stops after the job has finished.
        }
        catch (TimeoutException) when (work.IsCancellationRequested)
        {
            // The watchdog already cancelled the job and reported the stalled renewal.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Scheduled job {JobName} lease renewal failed", name);
            await work.CancelAsync();
        }
    }

    private async Task<bool> TryAcquireLease(CancellationToken cancellationToken)
    {
        var acquired = false;
        try
        {
            return await BackgroundOperationWatchdog.Run(
                async token =>
                {
                    acquired = await leaseService.TryAcquire(_leaseDuration, token);
                    token.ThrowIfCancellationRequested();

                    return acquired;
                },
                s_leaseOperationTimeout,
                () =>
                    logger.LogWarning(
                        "Scheduled job {JobName} lease acquisition stalled; cancelling and draining the call",
                        name
                    ),
                cancellationToken,
                timeProvider,
                rejectLateResult: true
            );
        }
        catch
        {
            if (acquired)
                await ReleaseLease();

            throw;
        }
    }

    private async Task ReleaseLease()
    {
        try
        {
            await BackgroundOperationWatchdog.Run(
                leaseService.Release,
                s_leaseOperationTimeout,
                () =>
                    logger.LogWarning(
                        "Scheduled job {JobName} lease release stalled; cancelling and draining the call",
                        name
                    ),
                CancellationToken.None,
                timeProvider
            );
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Scheduled job {JobName} lease could not be released; it will expire automatically",
                name
            );
        }
    }
}
