using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Defra.WasteObligations.Api.Services.Scheduling;

public class ScheduledJobProcessingWorker<TWork>(
    string name,
    IServiceScopeFactory scopeFactory,
    ScheduledJobProcessingOptions options,
    TimeProvider timeProvider,
    Func<string, IScheduledJobLeaseService> leaseFactory,
    ILogger<ScheduledJobProcessingWorker<TWork>> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                HashSet<string> excludedIds = [];
                while (await ProcessNext(excludedIds, stoppingToken))
                {
                    // Drain available work before waiting for the next polling interval.
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Scheduled job {JobName} processing failed", name);
            }

            await Task.Delay(TimeSpan.FromSeconds(options.PollIntervalSeconds), timeProvider, stoppingToken);
        }
    }

    private async Task<bool> ProcessNext(HashSet<string> excludedIds, CancellationToken stoppingToken)
    {
        ScheduledJobWork<TWork>? work;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            work = await scope
                .ServiceProvider.GetRequiredService<IScheduledJobWorkSource<TWork>>()
                .GetNext(excludedIds, stoppingToken);
        }
        if (work is null)
            return false;

        var completed = false;
        var execution = new ScheduledJobExecutionRunner(
            $"{name}:{work.Id}",
            leaseFactory(work.Id),
            timeProvider,
            logger
        );
        await execution.Run(
            scopeFactory,
            async (services, token) =>
            {
                var source = services.GetRequiredService<IScheduledJobWorkSource<TWork>>();
                if (!await source.TryStart(work, token))
                    return;

                token.ThrowIfCancellationRequested();
                await services.GetRequiredService<IScheduledJobProcessor<TWork>>().Process(work.Payload, token);
                token.ThrowIfCancellationRequested();
                await source.Complete(work, token);
                completed = true;
                logger.LogInformation("Scheduled job {JobName} completed instruction {InstructionId}", name, work.Id);
            },
            stoppingToken
        );

        if (!completed)
            excludedIds.Add(work.Id);

        return true;
    }
}
