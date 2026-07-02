using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Consumers;

public class AnalyticsAuditEventConsumer(
    IAmazonSQS sqsClient,
    IOptions<AnalyticsAuditEventConsumerOptions> options,
    ILogger<AnalyticsAuditEventConsumer> logger
) : BackgroundService
{
    private const string ContentEncodingHeader = "Content-Encoding";
    private const string GzipBase64ContentEncoding = "gzip+base64";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.ProcessingEnabled)
        {
            logger.LogInformation("Analytics audit event consumption is off");

            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);

            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await sqsClient.ReceiveMessageAsync(
                    new ReceiveMessageRequest
                    {
                        QueueUrl = options.Value.QueueUrl,
                        MaxNumberOfMessages = options.Value.BatchSize,
                        MessageAttributeNames = ["All"],
                        WaitTimeSeconds = options.Value.WaitTimeSeconds,
                    },
                    stoppingToken
                );

                if (response.Messages is not null)
                {
                    foreach (var message in response.Messages)
                    {
                        var (eventId, entityId) = ReadMessage(message);
                        logger.LogInformation(
                            "Consumed analytics audit event {EventId} for {EntityId}",
                            eventId,
                            entityId
                        );

                        await sqsClient.DeleteMessageAsync(
                            options.Value.QueueUrl,
                            message.ReceiptHandle,
                            stoppingToken
                        );
                    }
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Analytics audit event consumption failed");
            }

            await Delay(stoppingToken);
        }
    }

    private Task Delay(CancellationToken cancellationToken) =>
        Task.Delay(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds), cancellationToken);

    private static (string? EventId, string? EntityId) ReadMessage(Message message)
    {
        using var document = JsonDocument.Parse(ReadBody(message));
        var root = document.RootElement;

        return (
            root.TryGetProperty("eventId", out var eventId) ? eventId.GetString() : null,
            root.TryGetProperty("entityId", out var entityId) ? entityId.GetString() : null
        );
    }

    private static string ReadBody(Message message)
    {
        if (
            message.MessageAttributes is null
            || !message.MessageAttributes.TryGetValue(ContentEncodingHeader, out var contentEncoding)
            || contentEncoding.StringValue is null
        )
        {
            return message.Body;
        }

        if (contentEncoding.StringValue != GzipBase64ContentEncoding)
        {
            throw new InvalidOperationException(
                $"Analytics audit event message content encoding '{contentEncoding.StringValue}' is not supported."
            );
        }

        var bytes = Convert.FromBase64String(message.Body);
        using var input = new MemoryStream(bytes);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);

        return reader.ReadToEnd();
    }
}
