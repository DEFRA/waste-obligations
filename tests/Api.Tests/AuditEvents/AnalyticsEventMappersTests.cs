using AwesomeAssertions;
using Defra.WasteObligations.AuditEvents;
using Defra.WasteObligations.AuditEvents.Entities;

namespace Defra.WasteObligations.Api.Tests.AuditEvents;

public class AnalyticsEventMappersTests
{
    [Fact]
    public void ToAnalyticsEvent_ShouldPreserveEventIdAndMapEntityIdAsEntityAndEntityId()
    {
        const string entity = "compliance_declaration";
        const string entityId = "6830b14f9d2a7c61f4e8b935";
        const string eventId = "01JZ8RXBMTY2K15SJB3PCFN3D5";

        var auditEvent = new AuditEvent
        {
            EventId = eventId,
            Sequence = 1,
            Entity = entity,
            EntityId = entityId,
            Operation = "insert",
            OccurredAt = DateTime.UtcNow,
            RecordedAt = DateTime.UtcNow,
            Actor = "user@example.com",
            Version = 1,
            SchemaVersion = "compliance_declaration.v1",
        };

        var result = auditEvent.ToAnalyticsEvent();

        result.EventId.Should().Be(eventId);
        result.Entity.Should().Be(auditEvent.Entity);
        result.EntityId.Should().Be($"{entity}_{entityId}");
        result.Operation.Should().Be(auditEvent.Operation);
        result.SchemaVersion.Should().Be(auditEvent.SchemaVersion);
    }
}
