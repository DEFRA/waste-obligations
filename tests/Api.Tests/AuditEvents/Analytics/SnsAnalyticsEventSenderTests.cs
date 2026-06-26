using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using AwesomeAssertions;
using Defra.WasteObligations.AuditEvents.Analytics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.AuditEvents.Analytics;

public class SnsAnalyticsEventSenderTests
{
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
        var analyticsEvent = new AnalyticsEvent
        {
            EventId = "event-1",
            Sequence = 1,
            Entity = "compliance_declaration",
            EntityId = "compliance_declaration_entity-1",
            Operation = "insert",
            OccurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            RecordedAt = new DateTime(2026, 1, 1, 0, 0, 1, DateTimeKind.Utc),
            Actor = "user@example.com",
            Version = 1,
            SchemaVersion = "compliance_declaration.v1",
        };

        await subject.Send(analyticsEvent, TestContext.Current.CancellationToken);

        request.Should().NotBeNull();
        request!.TopicArn.Should().Be(TopicArn);
        request.Message.Should().Be("serialized-message");
        serializer.Received(1).Serialize(analyticsEvent);
    }
}
