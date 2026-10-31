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
        var execution = new ScheduledJobExecutionRunner(name, leaseService, timeProvider, logger);
        await execution.Run(
            scopeFactory,
            async (services, token) =>
            {
                if (!await TryStart(scheduledAt, token))
                    return;

                token.ThrowIfCancellationRequested();
                logger.LogInformation("Scheduled job {JobName} started for {ScheduledAt}", name, scheduledAt);
                var job = (IScheduledJob)services.GetRequiredService(jobType);
                await job.Run(token);
                logger.LogInformation("Scheduled job {JobName} finished for {ScheduledAt}", name, scheduledAt);
            },
            stoppingToken
        );
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
}
