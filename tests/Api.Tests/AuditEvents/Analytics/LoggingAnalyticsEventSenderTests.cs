using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.AuditEvents.Analytics;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.AuditEvents.Analytics;

public class LoggingAnalyticsEventSenderTests
{
    private const string Entity = "compliance_declaration";

    [Fact]
    public async Task Send_ShouldComplete()
    {
        var subject = new LoggingAnalyticsEventSender(Substitute.For<ILogger<LoggingAnalyticsEventSender>>());
        var analyticsEvent = new AnalyticsEvent
        {
            EventId = "compliance_declaration_event-1",
            Sequence = 1,
            Entity = Entity,
            EntityId = "entity-1",
            Operation = "insert",
            OccurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            RecordedAt = new DateTime(2026, 1, 1, 0, 0, 1, DateTimeKind.Utc),
            Actor = "user@example.com",
            Version = 1,
            SchemaVersion = $"{Entity}.{ComplianceDeclaration.SchemaVersionValue}",
        };

        var act = () => subject.Send(analyticsEvent, TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }
}
