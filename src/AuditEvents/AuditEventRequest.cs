using MongoDB.Bson;

namespace Defra.WasteObligations.AuditEvents;

public record AuditEventRequest(
    string Actor,
    string Entity,
    AuditEventOperation Operation,
    string EntityId,
    int Version,
    BsonDocument? Before,
    BsonDocument? After,
    string SchemaVersion,
    DateTime OccurredAt
);
