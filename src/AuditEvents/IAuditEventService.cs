using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.WasteObligations.AuditEvents;

public interface IAuditEventService
{
    Task RecordEvent(
        IClientSessionHandle session,
        string actor,
        string entity,
        AuditEventOperation operation,
        string entityId,
        int version,
        BsonDocument? before,
        BsonDocument? after,
        string schemaVersion,
        DateTime occurredAt,
        CancellationToken cancellationToken
    );
}
