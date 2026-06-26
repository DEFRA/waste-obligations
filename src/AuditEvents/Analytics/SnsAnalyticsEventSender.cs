using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.AuditEvents.Analytics;

public class SnsAnalyticsEventSender(
    IAmazonSimpleNotificationService simpleNotificationService,
    IAnalyticsEventSerializer analyticsEventSerializer,
    ILogger<SnsAnalyticsEventSender> logger,
    IOptions<AnalyticsAuditEventProcessorOptions> options
) : IAnalyticsEventSender
{
    public async Task Send(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken)
    {
        var message = analyticsEventSerializer.Serialize(analyticsEvent);
        var request = new PublishRequest { TopicArn = options.Value.TopicArn, Message = message };

        await simpleNotificationService.PublishAsync(request, cancellationToken);
        logger.LogInformation("Published analytics event {EventId}", analyticsEvent.EventId);
    }
}
