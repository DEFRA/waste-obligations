using System.IO.Compression;
using System.Text;
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
    private const int MaxBodyBytes = 252 * 1024;

    public async Task Send(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken)
    {
        var started = TimeProvider.System.GetTimestamp();
        var queueUrl = options.Value.QueueUrl!;
        metrics.SqsPublishStarted(options.Value.ProcessName, queueUrl, analyticsEvent);
        try
        {
            var body = serializer.Serialize(analyticsEvent);
            var attributes = new Dictionary<string, MessageAttributeValue>
            {
                ["Content-Type"] = new() { DataType = "String", StringValue = "application/json" },
            };
            if (Encoding.UTF8.GetByteCount(body) > MaxBodyBytes)
            {
                using var output = new MemoryStream();
                using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
                    await gzip.WriteAsync(Encoding.UTF8.GetBytes(body), cancellationToken);
                body = Convert.ToBase64String(output.ToArray());
                attributes["Content-Encoding"] = new() { DataType = "String", StringValue = "gzip+base64" };
            }

            if (Encoding.UTF8.GetByteCount(body) > MaxBodyBytes)
                throw new InvalidOperationException("Analytics event message exceeds queue message size limit.");

            await sqsClient.SendMessageAsync(
                new SendMessageRequest
                {
                    QueueUrl = options.Value.QueueUrl,
                    MessageBody = body,
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
