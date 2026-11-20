using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.WasteObligations.AuditEvents.Metrics;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.AuditEvents.Analytics;

public class SqsAnalyticsEventSender(
    IAmazonSQS sqsClient,
    IAnalyticsEventSerializer serializer,
    IOptions<AnalyticsAuditEventProcessorOptions> options,
    IAuditEventMetrics metrics
) : IAnalyticsEventSender
{
    public async Task Send(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken)
    {
        var started = TimeProvider.System.GetTimestamp();
        var queueUrl = options.Value.QueueUrl!;
        metrics.SqsPublishStarted(options.Value.ProcessName, queueUrl, analyticsEvent);
        try
        {
            var message = AnalyticsMessage.Create(
                serializer.Serialize(analyticsEvent),
                AnalyticsMessage.SqsMaxBodyBytes,
                "SQS"
            );
            var attributes = new Dictionary<string, MessageAttributeValue>
            {
                [AnalyticsMessage.ContentTypeHeader] = new()
                {
                    DataType = AnalyticsMessage.StringDataType,
                    StringValue = AnalyticsMessage.JsonContentType,
                },
            };
            if (message.ContentEncoding is not null)
                attributes[AnalyticsMessage.ContentEncodingHeader] = new()
                {
                    DataType = AnalyticsMessage.StringDataType,
                    StringValue = message.ContentEncoding,
                };

            await sqsClient.SendMessageAsync(
                new SendMessageRequest
                {
                    QueueUrl = options.Value.QueueUrl,
                    MessageBody = message.Body,
                    MessageAttributes = attributes,
                },
                cancellationToken
            );
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            metrics.SqsPublishFaulted(options.Value.ProcessName, queueUrl, analyticsEvent, exception);
            throw;
        }
        finally
        {
            metrics.SqsPublishCompleted(
                options.Value.ProcessName,
                queueUrl,
                analyticsEvent,
                TimeProvider.System.GetElapsedTime(started).TotalMilliseconds,
                Math.Max(0, (TimeProvider.System.GetUtcNow() - analyticsEvent.RecordedAt).TotalMilliseconds)
            );
        }
    }
}
