using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.AuditEvents.Analytics;

public class AnalyticsAuditEventProcessor(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<AnalyticsAuditEventProcessorOptions> options,
    ILogger<AnalyticsAuditEventProcessor> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Delay(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var dispatchedCount = await Process(stoppingToken);

                if (dispatchedCount > 0)
                {
                    logger.LogInformation(
                        "Processed {DispatchedCount} audit events for {ProcessName}",
                        dispatchedCount,
                        options.Value.ProcessName
                    );
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Analytics audit event processing failed");
            }

            await Delay(stoppingToken);
        }
    }

    private async Task<int> Process(CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var auditEventLeaseService = scope.ServiceProvider.GetRequiredService<AuditEventLeaseService>();
        var auditEventDispatchService = scope.ServiceProvider.GetRequiredService<AuditEventDispatchService>();
        var analyticsEventSender = scope.ServiceProvider.GetRequiredService<IAnalyticsEventSender>();
        var leaseDuration = TimeSpan.FromSeconds(options.Value.LeaseDurationSeconds);
        var processName = options.Value.ProcessName;

        if (!await auditEventLeaseService.TryAcquire(processName, leaseDuration, cancellationToken))
            return 0;

        try
        {
            var auditEvents = await auditEventDispatchService.ReadUnsent(
                processName,
                options.Value.BatchSize,
                cancellationToken
            );
            var dispatchedCount = 0;

            foreach (var auditEvent in auditEvents)
            {
                if (!await auditEventLeaseService.TryRenew(processName, leaseDuration, cancellationToken))
                {
                    logger.LogWarning(
                        "Stopped analytics audit event processing because lease renewal failed for {ProcessName}",
                        processName
                    );

                    break;
                }

                await analyticsEventSender.Send(auditEvent.ToAnalyticsEvent(), cancellationToken);
                await auditEventDispatchService.MarkDispatched(processName, auditEvent, cancellationToken);
                dispatchedCount++;
            }

            return dispatchedCount;
        }
        finally
        {
            await auditEventLeaseService.Release(processName, cancellationToken);
        }
    }

    private Task Delay(CancellationToken cancellationToken)
    {
        var pollInterval = TimeSpan.FromSeconds(options.Value.PollIntervalSeconds);
        var jitter = TimeSpan.FromSeconds(Random.Shared.Next(0, options.Value.PollJitterSeconds + 1));

        return Task.Delay(pollInterval + jitter, cancellationToken);
    }
}
