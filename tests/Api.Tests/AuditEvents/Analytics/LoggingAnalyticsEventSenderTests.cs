using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.AuditEvents.Analytics;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.AuditEvents.Analytics;

public class LoggingAnalyticsEventSenderTests
{
    [Fact]
    public async Task Send_ShouldComplete()
    {
        var subject = new LoggingAnalyticsEventSender(Substitute.For<ILogger<LoggingAnalyticsEventSender>>());
        var analyticsEvent = AnalyticsEventFixture.ComplianceDeclaration("compliance_declaration_event-1").Create();

        var act = () => subject.Send(analyticsEvent, TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }
}
