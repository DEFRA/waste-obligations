using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.AuditEvents.Analytics;
using Defra.WasteObligations.Testing.Fixtures.Entities;

namespace Defra.WasteObligations.Api.Tests.AuditEvents.Analytics;

public class AnalyticsEventMappersTests
{
    [Fact]
    public void ToAnalyticsEvent_ShouldPreserveEventIdAndMapEntityIdAsEntityAndEntityId()
    {
        const string entity = "compliance_declaration";
        const string entityId = "6830b14f9d2a7c61f4e8b935";
        const string eventId = "01JZ8RXBMTY2K15SJB3PCFN3D5";

        var auditEvent = AuditEventFixture
            .ComplianceDeclaration(eventId)
            .With(x => x.Entity, entity)
            .With(x => x.EntityId, entityId)
            .With(x => x.SchemaVersion, ComplianceDeclaration.SchemaVersionValue)
            .Create();

        var result = auditEvent.ToAnalyticsEvent();

        result.EventId.Should().Be(eventId);
        result.Entity.Should().Be(auditEvent.Entity);
        result.EntityId.Should().Be($"{entity}_{entityId}");
        result.Operation.Should().Be(auditEvent.Operation);
        result.EventType.Should().Be(auditEvent.EventType);
        result.DeletedReason.Should().Be(auditEvent.DeletedReason);
        result.PiiKeyRef.Should().BeNull();
        result.SchemaVersion.Should().Be($"{entity}.{ComplianceDeclaration.SchemaVersionValue}");
    }
}
