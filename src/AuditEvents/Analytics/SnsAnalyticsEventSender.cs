using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.AuditEvents.Analytics;

public class SnsAnalyticsEventSender(
    IAmazonSimpleNotificationService simpleNotificationService,
    IAnalyticsEventSerializer analyticsEventSerializer,
    IOptions<AnalyticsAuditEventProcessorOptions> options
) : IAnalyticsEventSender
{
    public async Task Send(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken)
    {
        var serializedMessage = analyticsEventSerializer.Serialize(analyticsEvent);
        var message = AnalyticsMessage.Create(serializedMessage, AnalyticsMessage.SnsMaxBodyBytes, "SNS");
        var request = new PublishRequest
        {
            TopicArn = options.Value.TopicArn,
            Message = message.Body,
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                [AnalyticsMessage.ContentTypeHeader] = new()
                {
                    DataType = AnalyticsMessage.StringDataType,
                    StringValue = AnalyticsMessage.JsonContentType,
                },
            },
        };

        if (message.ContentEncoding is not null)
        {
            request.MessageAttributes[AnalyticsMessage.ContentEncodingHeader] = new()
            {
                DataType = AnalyticsMessage.StringDataType,
                StringValue = message.ContentEncoding,
            };
        }

        await simpleNotificationService.PublishAsync(request, cancellationToken);
    }
}
