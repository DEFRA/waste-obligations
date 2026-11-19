using System.IO.Compression;
using System.Text;
using Amazon.SQS;
using Amazon.SQS.Model;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.AuditEvents.Analytics;
using Defra.WasteObligations.AuditEvents.Metrics;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.AuditEvents.Analytics;

public class SqsAnalyticsEventSenderTests
{
    [Fact]
    public async Task Send_ShouldSendSerializedEventDirectlyToConfiguredQueue()
    {
        const string queueUrl = "https://example.test/queue";
        SendMessageRequest? sent = null;
        var sqs = Substitute.For<IAmazonSQS>();
        sqs.SendMessageAsync(Arg.Do<SendMessageRequest>(x => sent = x), Arg.Any<CancellationToken>())
            .Returns(new SendMessageResponse());
        var serializer = Substitute.For<IAnalyticsEventSerializer>();
        serializer.Serialize(Arg.Any<AnalyticsEvent>()).Returns("{\"eventId\":\"event-1\"}");
        var subject = new SqsAnalyticsEventSender(
            sqs,
            serializer,
            Options.Create(
                new AnalyticsAuditEventProcessorOptions { ProcessName = "calculation", QueueUrl = queueUrl }
            ),
            Substitute.For<IAuditEventMetrics>()
        );

        await subject.Send(
            AnalyticsEventFixture.ComplianceDeclaration().Create(),
            TestContext.Current.CancellationToken
        );

        sent.Should().NotBeNull();
        sent.QueueUrl.Should().Be(queueUrl);
        sent.MessageBody.Should().Be("{\"eventId\":\"event-1\"}");
        sent.MessageAttributes["Content-Type"].StringValue.Should().Be("application/json");
    }

    [Fact]
    public async Task Send_WhenMessageFitsQueueLimitButExceedsTopicLimit_ShouldSendUncompressed()
    {
        var payload = new string('a', 512 * 1024);
        SendMessageRequest? sent = null;
        var sqs = Substitute.For<IAmazonSQS>();
        sqs.SendMessageAsync(Arg.Do<SendMessageRequest>(x => sent = x), Arg.Any<CancellationToken>())
            .Returns(new SendMessageResponse());
        var serializer = Substitute.For<IAnalyticsEventSerializer>();
        serializer.Serialize(Arg.Any<AnalyticsEvent>()).Returns(payload);
        var subject = new SqsAnalyticsEventSender(
            sqs,
            serializer,
            Options.Create(
                new AnalyticsAuditEventProcessorOptions
                {
                    ProcessName = "calculation",
                    QueueUrl = "https://example.test/queue",
                }
            ),
            Substitute.For<IAuditEventMetrics>()
        );

        await subject.Send(
            AnalyticsEventFixture.ComplianceDeclaration().Create(),
            TestContext.Current.CancellationToken
        );

        sent.Should().NotBeNull();
        sent.MessageBody.Should().Be(payload);
        sent.MessageAttributes.Should().NotContainKey("Content-Encoding");
    }

    [Fact]
    public async Task Send_WhenMessageIsLarge_ShouldCompressAndPreserveContent()
    {
        var payload = new string('a', 1021 * 1024);
        SendMessageRequest? sent = null;
        var sqs = Substitute.For<IAmazonSQS>();
        sqs.SendMessageAsync(Arg.Do<SendMessageRequest>(x => sent = x), Arg.Any<CancellationToken>())
            .Returns(new SendMessageResponse());
        var serializer = Substitute.For<IAnalyticsEventSerializer>();
        serializer.Serialize(Arg.Any<AnalyticsEvent>()).Returns(payload);
        var subject = new SqsAnalyticsEventSender(
            sqs,
            serializer,
            Options.Create(
                new AnalyticsAuditEventProcessorOptions
                {
                    ProcessName = "calculation",
                    QueueUrl = "https://example.test/queue",
                }
            ),
            Substitute.For<IAuditEventMetrics>()
        );

        await subject.Send(
            AnalyticsEventFixture.ComplianceDeclaration().Create(),
            TestContext.Current.CancellationToken
        );

        sent.Should().NotBeNull();
        sent.MessageAttributes["Content-Encoding"].StringValue.Should().Be("gzip+base64");
        using var stream = new MemoryStream(Convert.FromBase64String(sent.MessageBody));
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).Should().Be(payload);
    }

    [Fact]
    public async Task Send_WhenCompressedMessageIsTooLarge_ShouldRejectBeforeSending()
    {
        var bytes = new byte[1100 * 1024];
        new Random(42).NextBytes(bytes);
        var serializer = Substitute.For<IAnalyticsEventSerializer>();
        serializer.Serialize(Arg.Any<AnalyticsEvent>()).Returns(Convert.ToBase64String(bytes));
        var sqs = Substitute.For<IAmazonSQS>();
        var metrics = Substitute.For<IAuditEventMetrics>();
        var subject = new SqsAnalyticsEventSender(
            sqs,
            serializer,
            Options.Create(
                new AnalyticsAuditEventProcessorOptions
                {
                    ProcessName = "calculation",
                    QueueUrl = "https://example.test/queue",
                }
            ),
            metrics
        );

        var action = () =>
            subject.Send(AnalyticsEventFixture.ComplianceDeclaration().Create(), TestContext.Current.CancellationToken);

        await action
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Analytics event message exceeds the SQS message size limit.");
        metrics
            .Received(1)
            .SqsPublishFaulted(
                "calculation",
                "https://example.test/queue",
                Arg.Any<AnalyticsEvent>(),
                Arg.Any<InvalidOperationException>()
            );
        metrics
            .Received(1)
            .SqsPublishCompleted(
                "calculation",
                "https://example.test/queue",
                Arg.Any<AnalyticsEvent>(),
                Arg.Any<double>(),
                Arg.Any<double>()
            );
        await sqs.DidNotReceive().SendMessageAsync(Arg.Any<SendMessageRequest>(), Arg.Any<CancellationToken>());
    }
}
