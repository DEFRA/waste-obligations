using System.IO.Compression;
using System.Text;
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
    private const int SnsMessageLimitBytes = 256 * 1024;
    private const int MessageSizeBufferBytes = 4 * 1024;
    private const int MaxMessageBodyBytes = SnsMessageLimitBytes - MessageSizeBufferBytes;
    private const string ContentTypeHeader = "Content-Type";
    private const string JsonContentType = "application/json";
    private const string StringDataType = "String";

    public async Task Send(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken)
    {
        var serializedMessage = analyticsEventSerializer.Serialize(analyticsEvent);
        var message = CreateMessage(serializedMessage);
        var request = new PublishRequest
        {
            TopicArn = options.Value.TopicArn,
            Message = message.Body,
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                [ContentTypeHeader] = new() { DataType = StringDataType, StringValue = message.ContentType },
            },
        };

        if (message.ContentEncoding is not null)
        {
            request.MessageAttributes["Content-Encoding"] = new()
            {
                DataType = StringDataType,
                StringValue = message.ContentEncoding,
            };
        }

        await simpleNotificationService.PublishAsync(request, cancellationToken);
        logger.LogInformation("Published analytics event {EventId}", analyticsEvent.EventId);
    }

    private static AnalyticsMessage CreateMessage(string serializedMessage)
    {
        if (Encoding.UTF8.GetByteCount(serializedMessage) <= MaxMessageBodyBytes)
        {
            return new AnalyticsMessage(serializedMessage, JsonContentType);
        }

        var compressedMessage = Compress(serializedMessage);

        if (Encoding.UTF8.GetByteCount(compressedMessage) > MaxMessageBodyBytes)
        {
            throw new InvalidOperationException("Analytics event message exceeds the SNS message size limit.");
        }

        return new AnalyticsMessage(compressedMessage, JsonContentType, "gzip+base64");
    }

    private static string Compress(string serializedMessage)
    {
        var bytes = Encoding.UTF8.GetBytes(serializedMessage);
        using var output = new MemoryStream();

        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize))
        {
            gzip.Write(bytes);
        }

        return Convert.ToBase64String(output.ToArray());
    }

    private sealed record AnalyticsMessage(string Body, string ContentType, string? ContentEncoding = null);
}
