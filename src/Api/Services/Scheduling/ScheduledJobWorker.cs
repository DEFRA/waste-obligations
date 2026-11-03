using Cronos;
using Defra.WasteObligations.Api.Data;
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
    private const int InitialRetryDelaySeconds = 5;
    private const int MaximumRetryDelaySeconds = 60;
    private static readonly TimeSpan s_maximumWait = TimeSpan.FromDays(1);
    private static readonly TimeSpan s_retryDelay = TimeSpan.FromSeconds(InitialRetryDelaySeconds);

    private readonly IMongoCollection<ScheduledJobState> _states = database.GetCollection<ScheduledJobState>(
        "_scheduled_job_state"
    );

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DateTime? nextDue = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            var next = nextDue ?? schedule.GetNextOccurrence(timeProvider.GetUtcNow().UtcDateTime);
            nextDue = null;
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

                nextDue = await RunOccurrence(scheduledAt, stoppingToken);
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

    private async Task<DateTime?> RunOccurrence(DateTime scheduledAt, CancellationToken stoppingToken)
    {
        var nextOccurrence = schedule.GetNextOccurrence(scheduledAt);
        var attempted = false;
        var consecutiveFailures = 0;
        while (true)
        {
            if (attempted && nextOccurrence is { } following && timeProvider.GetUtcNow().UtcDateTime >= following)
                return following;

            var retryDelay = s_retryDelay;
            try
            {
                attempted = true;
                if (await Run(scheduledAt, stoppingToken))
                    return null;
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                consecutiveFailures = Math.Min(consecutiveFailures + 1, 5);
                retryDelay = FailureRetryDelay(exception, scheduledAt, consecutiveFailures);
            }

            var remaining = nextOccurrence is null
                ? retryDelay
                : nextOccurrence.Value - timeProvider.GetUtcNow().UtcDateTime;
            if (remaining > TimeSpan.Zero)
                await Task.Delay(remaining < retryDelay ? remaining : retryDelay, timeProvider, stoppingToken);
        }
    }

    private TimeSpan FailureRetryDelay(Exception exception, DateTime scheduledAt, int consecutiveFailures)
    {
        if (consecutiveFailures == 1)
            logger.LogError(
                exception,
                "Scheduled job {JobName} handoff failed for {ScheduledAt}; retrying",
                name,
                scheduledAt
            );
        else
            logger.LogWarning(
                "Scheduled job {JobName} handoff still failing for {ScheduledAt}: {FailureMessage}",
                name,
                scheduledAt,
                exception.Message
            );

        var baseSeconds = Math.Min(MaximumRetryDelaySeconds, InitialRetryDelaySeconds << (consecutiveFailures - 1));
        var delaySeconds = baseSeconds switch
        {
            InitialRetryDelaySeconds => InitialRetryDelaySeconds,
            MaximumRetryDelaySeconds => Random.Shared.Next(MaximumRetryDelaySeconds * 4 / 5, MaximumRetryDelaySeconds),
            _ => baseSeconds + Random.Shared.Next(baseSeconds / 5 + 1),
        };

        return TimeSpan.FromSeconds(delaySeconds);
    }

    private async Task<bool> Run(DateTime scheduledAt, CancellationToken stoppingToken)
    {
        var execution = new ScheduledJobExecutionRunner(name, leaseService, timeProvider, logger);
        var settled = false;
        try
        {
            await execution.Run(
                scopeFactory,
                async (services, token) =>
                {
                    var job = (IScheduledJob)services.GetRequiredService(jobType);
                    var handedOff = await services
                        .GetRequiredService<IDbContext>()
                        .ExecuteTransaction(
                            async (session, transactionToken) =>
                            {
                                var state = await _states
                                    .Find(session, x => x.Id == name)
                                    .FirstOrDefaultAsync(transactionToken);
                                if (state is not null && state.LastStartedAt >= scheduledAt)
                                    return false;

                                await job.Run(session, transactionToken);
                                await _states.UpdateOneAsync(
                                    session,
                                    x => x.Id == name,
                                    Builders<ScheduledJobState>.Update.Set(x => x.LastStartedAt, scheduledAt),
                                    new UpdateOptions { IsUpsert = true },
                                    transactionToken
                                );

                                return true;
                            },
                            "scheduled job handoff",
                            token
                        );
                    settled = true;
                    if (handedOff)
                        logger.LogInformation(
                            "Scheduled job {JobName} handed off for {ScheduledAt}",
                            name,
                            scheduledAt
                        );
                },
                stoppingToken
            );
        }
        catch (Exception exception) when (settled && !stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(
                exception,
                "Scheduled job {JobName} handoff settled for {ScheduledAt}, but cleanup failed",
                name,
                scheduledAt
            );
        }

        return settled;
    }
}
