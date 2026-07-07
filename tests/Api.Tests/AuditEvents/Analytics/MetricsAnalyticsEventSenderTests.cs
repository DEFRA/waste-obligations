using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.AuditEvents.Analytics;
using Defra.WasteObligations.AuditEvents.Metrics;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.AuditEvents.Analytics;

public class MetricsAnalyticsEventSenderTests
{
    private const string ProcessName = "analytics";
    private const string TopicArn = "arn:aws:sns:eu-west-2:000000000000:waste_obligations_analytics_events";

    [Fact]
    public async Task Send_WhenInnerSenderSucceeds_ShouldRecordStartedAndCompleted()
    {
        var innerSender = Substitute.For<IAnalyticsEventSender>();
        var auditEventMetrics = Substitute.For<IAuditEventMetrics>();
        var subject = CreateSubject(innerSender, auditEventMetrics);
        var analyticsEvent = AnalyticsEventFixture.ComplianceDeclaration().Create();

        await subject.Send(analyticsEvent, TestContext.Current.CancellationToken);

        await innerSender.Received(1).Send(analyticsEvent, TestContext.Current.CancellationToken);
        auditEventMetrics.Received(1).SnsPublishStarted(ProcessName, TopicArn, analyticsEvent);
        auditEventMetrics
            .Received(1)
            .SnsPublishCompleted(ProcessName, TopicArn, analyticsEvent, Arg.Any<double>(), Arg.Any<double>());
        auditEventMetrics
            .DidNotReceive()
            .SnsPublishFaulted(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<AnalyticsEvent>(), Arg.Any<Exception>());
    }

    [Fact]
    public async Task Send_WhenInnerSenderThrows_ShouldRecordFaultedAndCompleted()
    {
        var exception = new InvalidOperationException("Failed");
        var innerSender = Substitute.For<IAnalyticsEventSender>();
        innerSender
            .Send(Arg.Any<AnalyticsEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(exception));
        var auditEventMetrics = Substitute.For<IAuditEventMetrics>();
        var subject = CreateSubject(innerSender, auditEventMetrics);
        var analyticsEvent = AnalyticsEventFixture.ComplianceDeclaration().Create();

        var act = async () => await subject.Send(analyticsEvent, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Failed");
        auditEventMetrics.Received(1).SnsPublishStarted(ProcessName, TopicArn, analyticsEvent);
        auditEventMetrics.Received(1).SnsPublishFaulted(ProcessName, TopicArn, analyticsEvent, exception);
        auditEventMetrics
            .Received(1)
            .SnsPublishCompleted(ProcessName, TopicArn, analyticsEvent, Arg.Any<double>(), Arg.Any<double>());
    }

    private static MetricsAnalyticsEventSender CreateSubject(
        IAnalyticsEventSender innerSender,
        IAuditEventMetrics auditEventMetrics
    ) =>
        new(
            innerSender,
            auditEventMetrics,
            Options.Create(new AnalyticsAuditEventProcessorOptions { ProcessName = ProcessName, TopicArn = TopicArn })
        );
}
