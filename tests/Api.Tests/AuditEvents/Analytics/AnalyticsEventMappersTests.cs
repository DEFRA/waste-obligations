using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.AuditEvents.Analytics;
using Defra.WasteObligations.Testing.Fixtures.Entities;

namespace Defra.WasteObligations.Api.Tests.AuditEvents.Analytics;

public class AnalyticsEventMappersTests
{
    [Fact]
    public void ToAnalyticsEvent_ShouldMapTheOutboxEventToTheGovernedEnvelope()
    {
        const string entity = "compliance_declaration";
        const string entityId = "6830b14f9d2a7c61f4e8b935";
        const string eventId = "01JZ8RXBMTY2K15SJB3PCFN3D5";
        const string traceId = "cdp-request-id";

        var auditEvent = AuditEventFixture
            .ComplianceDeclaration(eventId)
            .With(x => x.Entity, entity)
            .With(x => x.EntityId, entityId)
            .With(x => x.SchemaVersion, ComplianceDeclaration.SchemaVersionValue)
            .With(x => x.Actor, "service:waste-obligations")
            .With(x => x.TraceId, traceId)
            .Create();

        var result = auditEvent.ToAnalyticsEvent();

        result.EventId.Should().Be(eventId);
        result.Entity.Should().Be(auditEvent.Entity);
        result.EntityId.Should().Be($"{entity}_{entityId}");
        result.Operation.Should().Be("create");
        result.EventType.Should().Be(auditEvent.EventType);
        result.DeletedReason.Should().Be(auditEvent.DeletedReason);
        result.PiiKeyRef.Should().BeNull();
        result.CorrelationId.Should().Be(traceId);
        result.SchemaVersion.Should().Be($"{entity}_{ComplianceDeclaration.SchemaVersionValue}");
    }

    [Fact]
    public void ToAnalyticsEvent_WhenOutboxOperationIsUpdate_ShouldRetainUpdate()
    {
        var auditEvent = AuditEventFixture
            .ComplianceDeclaration()
            .With(x => x.Operation, "update")
            .With(x => x.EventType, "submission.amended")
            .With(x => x.Actor, "service:waste-obligations")
            .Create();

        var result = auditEvent.ToAnalyticsEvent();

        result.Operation.Should().Be("update");
    }

    [Fact]
    public void ToAnalyticsEvent_WhenLegacyDeletionReason_ShouldMapToTheGovernedValue()
    {
        var auditEvent = AuditEventFixture
            .ComplianceDeclaration()
            .With(x => x.Operation, "delete")
            .With(x => x.EventType, "submission.removed")
            .With(x => x.DeletedReason, "elevated system allowed removal")
            .With(x => x.Actor, "service:waste-obligations")
            .Create();

        var result = auditEvent.ToAnalyticsEvent();

        result.DeletedReason.Should().Be(AnalyticsEventVocabulary.ElevatedSystemAllowedRemoval);
    }

    [Fact]
    public void ToAnalyticsEvent_WhenEntityIsUnregistered_ShouldThrow()
    {
        var auditEvent = AuditEventFixture.ComplianceDeclaration().With(x => x.Entity, "unknown_entity").Create();

        var act = () => auditEvent.ToAnalyticsEvent();

        act.Should().Throw<InvalidOperationException>().WithMessage("Unregistered analytics entity 'unknown_entity'.");
    }

    [Fact]
    public void ToAnalyticsEvent_WhenOperationIsUnregistered_ShouldThrow()
    {
        var auditEvent = AuditEventFixture.ComplianceDeclaration().With(x => x.Operation, "merge").Create();

        var act = () => auditEvent.ToAnalyticsEvent();

        act.Should().Throw<InvalidOperationException>().WithMessage("Unregistered analytics operation 'merge'.");
    }

    [Fact]
    public void Validate_WhenOperationIsUnregistered_ShouldThrow()
    {
        var analyticsEvent = AnalyticsEventFixture.ComplianceDeclaration().With(x => x.Operation, "merge").Create();

        var act = () => AnalyticsEventVocabulary.Validate(analyticsEvent);

        act.Should().Throw<InvalidOperationException>().WithMessage("Unregistered analytics operation 'merge'.");
    }

    [Fact]
    public void ToAnalyticsEvent_WhenEventTypeIsUnregistered_ShouldThrow()
    {
        var auditEvent = AuditEventFixture
            .ComplianceDeclaration()
            .With(x => x.EventType, "submission.unknown")
            .Create();

        var act = () => auditEvent.ToAnalyticsEvent();

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Unregistered analytics event type 'submission.unknown'.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("service:")]
    [InlineData("unknown:actor")]
    public void ToAnalyticsEvent_WhenActorIsUnregistered_ShouldThrow(string actor)
    {
        var auditEvent = AuditEventFixture.ComplianceDeclaration().With(x => x.Actor, actor).Create();

        var act = () => auditEvent.ToAnalyticsEvent();

        act.Should().Throw<InvalidOperationException>().WithMessage($"Unregistered analytics actor '{actor}'.");
    }

    [Fact]
    public void ToAnalyticsEvent_WhenDeletionReasonIsUnregistered_ShouldThrow()
    {
        var auditEvent = AuditEventFixture
            .ComplianceDeclaration()
            .With(x => x.Operation, "delete")
            .With(x => x.EventType, "submission.removed")
            .With(x => x.DeletedReason, "unknown_reason")
            .With(x => x.Actor, "service:waste-obligations")
            .Create();

        var act = () => auditEvent.ToAnalyticsEvent();

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Unregistered analytics deletion reason 'unknown_reason'.");
    }

    [Fact]
    public void ToAnalyticsEvent_WhenDeletionReasonIsSetForANonDeleteOperation_ShouldThrow()
    {
        var auditEvent = AuditEventFixture
            .ComplianceDeclaration()
            .With(x => x.DeletedReason, AnalyticsEventVocabulary.ElevatedSystemAllowedRemoval)
            .With(x => x.Actor, "service:waste-obligations")
            .Create();

        var act = () => auditEvent.ToAnalyticsEvent();

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("An analytics deletion reason is only valid for delete operations.");
    }

    [Fact]
    public void ToAnalyticsEvent_WhenSchemaVersionIsUnregistered_ShouldThrow()
    {
        var auditEvent = AuditEventFixture
            .ComplianceDeclaration()
            .With(x => x.Actor, "service:waste-obligations")
            .With(x => x.SchemaVersion, "v2.0")
            .Create();

        var act = () => auditEvent.ToAnalyticsEvent();

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Unregistered analytics schema version 'compliance_declaration_v2.0'.");
    }
}
