using System.IO.Compression;
using System.Text;
using Amazon.SQS;
using Amazon.SQS.Model;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Consumers;
using Defra.WasteObligations.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.Core;

namespace Defra.WasteObligations.Api.Tests.Consumers;

public class AnalyticsAuditEventConsumerTests
{
    private const string EntityId = "compliance_declaration_65f1f6570bb08052a8a27b01";
    private const string EventId = "01JZ8RXBMTY2K15SJB3PCFN3D5";
    private const string QueueUrl = "http://localhost:4566/000000000000/waste_obligations_analytics_events_queue";
    private const string ReceiptHandle = "receipt-handle-1";

    [Fact]
    public async Task Start_WhenProcessingIsDisabled_ShouldLogAndNotReceive()
    {
        var sqsClient = Substitute.For<IAmazonSQS>();
        var logger = new RecordingLogger<AnalyticsAuditEventConsumer>();
        var subject = CreateSubject(sqsClient, logger, processingEnabled: false);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        logger.Messages.Should().ContainSingle("Analytics audit event consumption is off");
        await sqsClient
            .DidNotReceive()
            .ReceiveMessageAsync(Arg.Any<ReceiveMessageRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_WhenMessageReceived_ShouldLogAndDeleteMessage()
    {
        ReceiveMessageRequest? request = null;
        var deleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sqsClient = Substitute.For<IAmazonSQS>();
        sqsClient
            .ReceiveMessageAsync(Arg.Do<ReceiveMessageRequest>(x => request = x), Arg.Any<CancellationToken>())
            .Returns(MessageThenWait(CreateMessage(Body())));
        sqsClient
            .DeleteMessageAsync(QueueUrl, ReceiptHandle, Arg.Any<CancellationToken>())
            .Returns(new DeleteMessageResponse())
            .AndDoes(_ => deleted.TrySetResult());
        var logger = new RecordingLogger<AnalyticsAuditEventConsumer>();
        var subject = CreateSubject(sqsClient, logger);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await deleted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        request.Should().NotBeNull();
        request.MessageAttributeNames.Should().Equal("All");
        logger.Messages.Should().Contain(x => x.Contains(EventId) && x.Contains(EntityId));
        await sqsClient.Received(1).DeleteMessageAsync(QueueUrl, ReceiptHandle, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_WhenNoMessagesReceived_ShouldNotLogErrorOrDeleteMessage()
    {
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sqsClient = Substitute.For<IAmazonSQS>();
        sqsClient
            .ReceiveMessageAsync(Arg.Any<ReceiveMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                received.TrySetResult();

                return Task.FromResult(new ReceiveMessageResponse());
            });
        var logger = new RecordingLogger<AnalyticsAuditEventConsumer>();
        var subject = CreateSubject(sqsClient, logger, pollIntervalSeconds: 30);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        logger.Entries.Should().NotContain(x => x.Level == LogLevel.Error);
        await sqsClient
            .DidNotReceive()
            .DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_WhenCompressedMessageReceived_ShouldDecompressLogAndDeleteMessage()
    {
        var deleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sqsClient = Substitute.For<IAmazonSQS>();
        sqsClient
            .ReceiveMessageAsync(Arg.Any<ReceiveMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(MessageThenWait(CreateMessage(Compress(Body()), contentEncoding: "gzip+base64")));
        sqsClient
            .DeleteMessageAsync(QueueUrl, ReceiptHandle, Arg.Any<CancellationToken>())
            .Returns(new DeleteMessageResponse())
            .AndDoes(_ => deleted.TrySetResult());
        var logger = new RecordingLogger<AnalyticsAuditEventConsumer>();
        var subject = CreateSubject(sqsClient, logger);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await deleted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        logger.Messages.Should().Contain(x => x.Contains(EventId) && x.Contains(EntityId));
        await sqsClient.Received(1).DeleteMessageAsync(QueueUrl, ReceiptHandle, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_WhenMessageContentEncodingIsUnsupported_ShouldLogErrorAndNotDeleteMessage()
    {
        const string contentEncoding = "br";
        var sqsClient = Substitute.For<IAmazonSQS>();
        sqsClient
            .ReceiveMessageAsync(Arg.Any<ReceiveMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(MessageThenWait(CreateMessage(Body(), contentEncoding: contentEncoding)));
        var logger = new RecordingLogger<AnalyticsAuditEventConsumer>();
        var subject = CreateSubject(sqsClient, logger);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await WaitForLog(logger, "Analytics audit event consumption failed");
        await subject.StopAsync(TestContext.Current.CancellationToken);

        logger
            .Entries.Should()
            .ContainSingle(x =>
                x.Level == LogLevel.Error
                && x.Message == "Analytics audit event consumption failed"
                && x.Exception is InvalidOperationException
                && x.Exception.Message.Contains(contentEncoding)
            );
        await sqsClient
            .DidNotReceive()
            .DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_WhenReceiveFails_ShouldLogError()
    {
        const string failureMessage = "SQS receive failed";
        var sqsClient = Substitute.For<IAmazonSQS>();
        sqsClient
            .ReceiveMessageAsync(Arg.Any<ReceiveMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(FailureThenWait(new InvalidOperationException(failureMessage)));
        var logger = new RecordingLogger<AnalyticsAuditEventConsumer>();
        var subject = CreateSubject(sqsClient, logger);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await WaitForLog(logger, "Analytics audit event consumption failed");
        await subject.StopAsync(TestContext.Current.CancellationToken);

        logger
            .Entries.Should()
            .ContainSingle(x =>
                x.Level == LogLevel.Error
                && x.Message == "Analytics audit event consumption failed"
                && x.Exception is InvalidOperationException
                && x.Exception.Message == failureMessage
            );
    }

    private static AnalyticsAuditEventConsumer CreateSubject(
        IAmazonSQS sqsClient,
        ILogger<AnalyticsAuditEventConsumer> logger,
        bool processingEnabled = true,
        int pollIntervalSeconds = 1
    ) =>
        new(
            sqsClient,
            Options.Create(
                new AnalyticsAuditEventConsumerOptions
                {
                    QueueUrl = QueueUrl,
                    ProcessingEnabled = processingEnabled,
                    BatchSize = 10,
                    WaitTimeSeconds = 0,
                    PollIntervalSeconds = pollIntervalSeconds,
                }
            ),
            logger
        );

    private static async Task WaitForLog(RecordingLogger<AnalyticsAuditEventConsumer> logger, string message)
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        while (!logger.Messages.Contains(message))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationTokenSource.Token);
        }
    }

    private static Func<CallInfo, Task<ReceiveMessageResponse>> MessageThenWait(Message message)
    {
        var receivedCount = 0;

        return call =>
        {
            if (Interlocked.Increment(ref receivedCount) == 1)
            {
                return Task.FromResult(new ReceiveMessageResponse { Messages = [message] });
            }

            var cancellationToken = call.ArgAt<CancellationToken>(1);

            return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
                .ContinueWith(_ => new ReceiveMessageResponse(), CancellationToken.None);
        };
    }

    private static Func<CallInfo, Task<ReceiveMessageResponse>> FailureThenWait(Exception exception)
    {
        var receivedCount = 0;

        return call =>
        {
            if (Interlocked.Increment(ref receivedCount) == 1)
            {
                return Task.FromException<ReceiveMessageResponse>(exception);
            }

            var cancellationToken = call.ArgAt<CancellationToken>(1);

            return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
                .ContinueWith(_ => new ReceiveMessageResponse(), CancellationToken.None);
        };
    }

    private static Message CreateMessage(string body, string? contentEncoding = null)
    {
        var message = new Message
        {
            Body = body,
            MessageAttributes = [],
            ReceiptHandle = ReceiptHandle,
        };

        if (contentEncoding is not null)
        {
            message.MessageAttributes["Content-Encoding"] = new()
            {
                DataType = "String",
                StringValue = contentEncoding,
            };
        }

        return message;
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
