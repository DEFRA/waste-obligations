using Defra.WasteObligations.AuditEvents.Entities;

namespace Defra.WasteObligations.AuditEvents.Analytics;

public static class AnalyticsEventMappers
{
    public static AnalyticsEvent ToAnalyticsEvent(this AuditEvent auditEvent) =>
        new()
        {
            EventId = auditEvent.EventId,
            Sequence = auditEvent.Sequence,
            Entity = auditEvent.Entity,
            EntityId = $"{auditEvent.Entity}_{auditEvent.EntityId}",
            Operation = auditEvent.Operation,
            EventType = auditEvent.EventType,
            OccurredAt = auditEvent.OccurredAt,
            RecordedAt = auditEvent.RecordedAt,
            Actor = auditEvent.Actor,
            Version = auditEvent.Version,
            Before = auditEvent.Before,
            After = auditEvent.After,
            SchemaVersion = $"{auditEvent.Entity}.{auditEvent.SchemaVersion}",
        };
}
