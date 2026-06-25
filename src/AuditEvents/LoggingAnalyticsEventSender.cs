using Microsoft.Extensions.Logging;

namespace Defra.WasteObligations.AuditEvents;

public class LoggingAnalyticsEventSender(ILogger<LoggingAnalyticsEventSender> logger) : IAnalyticsEventSender
{
    public Task Send(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Audit event {EventId} for {Entity} {EntityId} would be sent to analytics topic",
            analyticsEvent.EventId,
            analyticsEvent.Entity,
            analyticsEvent.EntityId
        );

        return Task.CompletedTask;
    }
}
