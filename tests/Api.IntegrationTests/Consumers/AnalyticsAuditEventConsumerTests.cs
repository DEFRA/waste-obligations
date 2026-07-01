using System.IO.Compression;
using System.Text;
using Amazon.SQS;
using Amazon.SQS.Model;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Consumers;
using Defra.WasteObligations.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.IntegrationTests.Consumers;

public class AnalyticsAuditEventConsumerTests : IntegrationTestBase
{
    private const string EntityId = "compliance_declaration_65f1f6570bb08052a8a27b01";
    private const string EventId = "01JZ8RXBMTY2K15SJB3PCFN3D5";

    [Fact]
    public async Task Start_WhenJsonMessageIsQueued_ShouldLogAndDeleteMessage()
    {
        using var sqsClient = CreateSqsClient();
        var logger = new RecordingLogger<AnalyticsAuditEventConsumer>();
        var subject = CreateSubject(sqsClient, logger);

        await sqsClient.SendMessageAsync(
            new SendMessageRequest
            {
                QueueUrl = AnalyticsEventsQueueUrl,
                MessageBody = Body(),
                MessageAttributes = JsonMessageAttributes(),
            },
            TestContext.Current.CancellationToken
        );

        await subject.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await WaitForMessageLogged(logger);
            await WaitForQueueToBeEmpty(sqsClient);
        }
        finally
        {
            await subject.StopAsync(TestContext.Current.CancellationToken);
        }

        logger.Messages.Should().Contain(x => x.Contains(EventId) && x.Contains(EntityId));
    }

    [Fact]
    public async Task Start_WhenCompressedMessageIsQueued_ShouldDecompressLogAndDeleteMessage()
    {
        using var sqsClient = CreateSqsClient();
        var logger = new RecordingLogger<AnalyticsAuditEventConsumer>();
        var subject = CreateSubject(sqsClient, logger);

        await sqsClient.SendMessageAsync(
            new SendMessageRequest
            {
                QueueUrl = AnalyticsEventsQueueUrl,
                MessageBody = Compress(Body()),
                MessageAttributes = JsonMessageAttributes(contentEncoding: "gzip+base64"),
            },
            TestContext.Current.CancellationToken
        );

        await subject.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await WaitForMessageLogged(logger);
            await WaitForQueueToBeEmpty(sqsClient);
        }
        finally
        {
            await subject.StopAsync(TestContext.Current.CancellationToken);
        }

        logger.Messages.Should().Contain(x => x.Contains(EventId) && x.Contains(EntityId));
    }

    private static AnalyticsAuditEventConsumer CreateSubject(
        IAmazonSQS sqsClient,
        ILogger<AnalyticsAuditEventConsumer> logger,
        int pollIntervalSeconds = 1
    ) =>
        new(
            sqsClient,
            Options.Create(
                new AnalyticsAuditEventConsumerOptions
                {
                    QueueUrl = AnalyticsEventsQueueUrl,
                    ProcessingEnabled = true,
                    BatchSize = 10,
                    WaitTimeSeconds = 1,
                    PollIntervalSeconds = pollIntervalSeconds,
                }
            ),
            logger
        );

    private static async Task WaitForMessageLogged(RecordingLogger<AnalyticsAuditEventConsumer> logger)
    {
        await AsyncWaiter.WaitForAsync(
            () =>
            {
                logger.Messages.Should().Contain(x => x.Contains(EventId) && x.Contains(EntityId));

                return Task.CompletedTask;
            },
            timeout: 5,
            delay: TimeSpan.FromMilliseconds(50)
        );
    }

    private static async Task WaitForQueueToBeEmpty(IAmazonSQS sqsClient)
    {
        await AsyncWaiter.WaitForAsync(
            async () =>
            {
                var response = await sqsClient.ReceiveMessageAsync(
                    new ReceiveMessageRequest
                    {
                        QueueUrl = AnalyticsEventsQueueUrl,
                        MaxNumberOfMessages = 1,
                        WaitTimeSeconds = 0,
                    },
                    TestContext.Current.CancellationToken
                );

                response.Messages.Should().BeNullOrEmpty();
            },
            timeout: 5,
            delay: TimeSpan.FromMilliseconds(50)
        );
    }

    private static Dictionary<string, MessageAttributeValue> JsonMessageAttributes(string? contentEncoding = null)
    {
        var attributes = new Dictionary<string, MessageAttributeValue>
        {
            ["Content-Type"] = new() { DataType = "String", StringValue = "application/json" },
        };

        if (contentEncoding is not null)
        {
            attributes["Content-Encoding"] = new() { DataType = "String", StringValue = contentEncoding };
        }

        return attributes;
    }

    private static string Body() =>
        $$"""
            {
              "eventId": "{{EventId}}",
              "entityId": "{{EntityId}}"
            }
            """;

    private static string Compress(string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        using var output = new MemoryStream();

        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize))
        {
            gzip.Write(bytes);
        }

        return Convert.ToBase64String(output.ToArray());
    }
}
