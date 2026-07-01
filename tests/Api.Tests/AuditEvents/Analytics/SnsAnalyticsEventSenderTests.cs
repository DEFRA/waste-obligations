using System.IO.Compression;
using System.Text;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.AuditEvents.Analytics;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.AuditEvents.Analytics;

public class SnsAnalyticsEventSenderTests
{
    private const int MaxMessageBodyBytes = (256 * 1024) - (4 * 1024);
    private const string ContentEncodingHeader = "Content-Encoding";
    private const string ContentTypeHeader = "Content-Type";
    private const string TopicArn = "arn:aws:sns:eu-west-2:000000000000:waste_obligations_analytics_events";

    [Fact]
    public async Task Send_ShouldPublishAnalyticsEventAsJson()
    {
        PublishRequest? request = null;
        var simpleNotificationService = Substitute.For<IAmazonSimpleNotificationService>();
        simpleNotificationService
            .PublishAsync(Arg.Do<PublishRequest>(x => request = x), Arg.Any<CancellationToken>())
            .Returns(new PublishResponse());
        var serializer = Substitute.For<IAnalyticsEventSerializer>();
        serializer.Serialize(Arg.Any<AnalyticsEvent>()).Returns("serialized-message");
        var subject = new SnsAnalyticsEventSender(
            simpleNotificationService,
            serializer,
            Substitute.For<ILogger<SnsAnalyticsEventSender>>(),
            Options.Create(new AnalyticsAuditEventProcessorOptions { ProcessName = "analytics", TopicArn = TopicArn })
        );
        var analyticsEvent = AnalyticsEventFixture.ComplianceDeclaration().Create();

        await subject.Send(analyticsEvent, TestContext.Current.CancellationToken);

        request.Should().NotBeNull();
        request!.TopicArn.Should().Be(TopicArn);
        request.Message.Should().Be("serialized-message");
        request.MessageAttributes.Should().ContainKey(ContentTypeHeader);
        request.MessageAttributes[ContentTypeHeader].StringValue.Should().Be("application/json");
        request.MessageAttributes.Should().NotContainKey(ContentEncodingHeader);
        serializer.Received(1).Serialize(analyticsEvent);
    }

    [Fact]
    public async Task Send_WhenMessageIsTooLarge_ShouldCompressMessage()
    {
        const int messageLength = MaxMessageBodyBytes + 1;

        PublishRequest? request = null;
        var simpleNotificationService = Substitute.For<IAmazonSimpleNotificationService>();
        simpleNotificationService
            .PublishAsync(Arg.Do<PublishRequest>(x => request = x), Arg.Any<CancellationToken>())
            .Returns(new PublishResponse());
        var serializer = Substitute.For<IAnalyticsEventSerializer>();
        var serializedMessage = new string('a', messageLength);
        serializer.Serialize(Arg.Any<AnalyticsEvent>()).Returns(serializedMessage);
        var subject = new SnsAnalyticsEventSender(
            simpleNotificationService,
            serializer,
            Substitute.For<ILogger<SnsAnalyticsEventSender>>(),
            Options.Create(new AnalyticsAuditEventProcessorOptions { ProcessName = "analytics", TopicArn = TopicArn })
        );
        var analyticsEvent = AnalyticsEventFixture.ComplianceDeclaration().Create();

        await subject.Send(analyticsEvent, TestContext.Current.CancellationToken);

        request.Should().NotBeNull();
        request!.Message.Should().NotBe(serializedMessage);
        request.MessageAttributes.Should().ContainKey(ContentTypeHeader);
        request.MessageAttributes[ContentTypeHeader].StringValue.Should().Be("application/json");
        request.MessageAttributes.Should().ContainKey(ContentEncodingHeader);
        request.MessageAttributes[ContentEncodingHeader].StringValue.Should().Be("gzip+base64");
        Decompress(request.Message).Should().Be(serializedMessage);
    }

    [Fact]
    public async Task Send_WhenCompressedMessageIsTooLarge_ShouldThrow()
    {
        const int messageLength = MaxMessageBodyBytes + 1;

        var simpleNotificationService = Substitute.For<IAmazonSimpleNotificationService>();
        var serializer = Substitute.For<IAnalyticsEventSerializer>();
        serializer.Serialize(Arg.Any<AnalyticsEvent>()).Returns(RandomString(messageLength));
        var subject = new SnsAnalyticsEventSender(
            simpleNotificationService,
            serializer,
            Substitute.For<ILogger<SnsAnalyticsEventSender>>(),
            Options.Create(new AnalyticsAuditEventProcessorOptions { ProcessName = "analytics", TopicArn = TopicArn })
        );
        var analyticsEvent = AnalyticsEventFixture.ComplianceDeclaration().Create();

        var act = async () => await subject.Send(analyticsEvent, TestContext.Current.CancellationToken);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Analytics event message exceeds the SNS message size limit.");
        await simpleNotificationService
            .DidNotReceive()
            .PublishAsync(Arg.Any<PublishRequest>(), Arg.Any<CancellationToken>());
    }

    private static string Decompress(string compressedMessage)
    {
        var bytes = Convert.FromBase64String(compressedMessage);
        using var input = new MemoryStream(bytes);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);

        return reader.ReadToEnd();
    }

    private static string RandomString(int length)
    {
        var random = new Random(123);
        var builder = new StringBuilder(length);

        for (var i = 0; i < length; i++)
        {
            builder.Append((char)random.Next(32, 127));
        }

        return builder.ToString();
    }
}
