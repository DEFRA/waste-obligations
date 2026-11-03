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
    private static readonly TimeSpan s_maximumWait = TimeSpan.FromDays(1);
    private static readonly TimeSpan s_retryDelay = TimeSpan.FromSeconds(5);

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

                var nextOccurrence = schedule.GetNextOccurrence(scheduledAt);
                var attempted = false;
                while (true)
                {
                    if (
                        attempted
                        && nextOccurrence is { } following
                        && timeProvider.GetUtcNow().UtcDateTime >= following
                    )
                    {
                        nextDue = following;
                        break;
                    }

                    try
                    {
                        attempted = true;
                        if (await Run(scheduledAt, stoppingToken))
                            break;
                    }
                    catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                    {
                        logger.LogError(
                            exception,
                            "Scheduled job {JobName} handoff failed for {ScheduledAt}; retrying",
                            name,
                            scheduledAt
                        );
                    }

                    var remaining = nextOccurrence is null
                        ? s_retryDelay
                        : nextOccurrence.Value - timeProvider.GetUtcNow().UtcDateTime;
                    if (remaining > TimeSpan.Zero)
                        await Task.Delay(
                            remaining < s_retryDelay ? remaining : s_retryDelay,
                            timeProvider,
                            stoppingToken
                        );
                }
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

    private async Task<bool> Run(DateTime scheduledAt, CancellationToken stoppingToken)
    {
        var execution = new ScheduledJobExecutionRunner(name, leaseService, timeProvider, logger);
        var settled = false;
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
                if (handedOff)
                    logger.LogInformation("Scheduled job {JobName} handed off for {ScheduledAt}", name, scheduledAt);
                settled = true;
            },
            stoppingToken
        );

        return settled;
    }
}
