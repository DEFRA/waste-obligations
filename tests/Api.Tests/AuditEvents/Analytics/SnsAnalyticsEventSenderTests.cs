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
        serializer.Received(1).Serialize(analyticsEvent);
    }
}
