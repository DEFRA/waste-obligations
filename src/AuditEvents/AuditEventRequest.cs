using MongoDB.Bson;

namespace Defra.WasteObligations.AuditEvents;

public record AuditEventRequest(
    string Actor,
    string Entity,
    AuditEventOperation Operation,
    string EventType,
    string EntityId,
    int Version,
    BsonDocument? Before,
    BsonDocument? After,
    string SchemaVersion,
    DateTime OccurredAt,
    string? TraceId = null
);
