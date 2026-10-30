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
    TimeProvider timeProvider,
    ILogger<ScheduledJobWorker> logger
) : BackgroundService
{
    private const int DuplicateKeyErrorCode = 11000;
    private static readonly TimeSpan s_maximumWait = TimeSpan.FromDays(1);

    private readonly TimeSpan _leaseDuration = TimeSpan.FromMinutes(1);

    private readonly BackgroundWorkerLeaseService _lease = new(database, timeProvider, "_scheduled_job_leases", name);
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
        if (!await _lease.TryAcquire(_leaseDuration, stoppingToken))
            return;

        using var work = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        using var renewal = new CancellationTokenSource();
        var renewalTask = RenewLease(work, renewal.Token);
        Exception? processingFailure = null;

        try
        {
            if (await TryStart(scheduledAt, work.Token))
            {
                work.Token.ThrowIfCancellationRequested();
                logger.LogInformation("Scheduled job {JobName} started for {ScheduledAt}", name, scheduledAt);
                await using var scope = scopeFactory.CreateAsyncScope();
                var job = (IScheduledJob)scope.ServiceProvider.GetRequiredService(jobType);
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
                work.CancelAsync,
                renewal.CancelAsync,
                () => renewalTask,
                () => _lease.Release(CancellationToken.None)
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
                if (await _lease.TryRenew(_leaseDuration, cancellationToken))
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
        catch (Exception exception)
        {
            logger.LogError(exception, "Scheduled job {JobName} lease renewal failed", name);
            await work.CancelAsync();
        }
    }
}
