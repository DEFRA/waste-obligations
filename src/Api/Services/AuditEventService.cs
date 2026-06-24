using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services;

public class AuditEventService(IDbContext dbContext, TimeProvider timeProvider, IEventIdGenerator eventIdGenerator)
    : IAuditEventService
{
    public async Task RecordEvent(
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
    )
    {
        var sequence = await AllocateSequence(session, cancellationToken);
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();

        await dbContext.AuditEvents.InsertOneAsync(
            session,
            new AuditEvent
            {
                EventId = eventIdGenerator.Generate(),
                Sequence = sequence,
                Entity = entity,
                EntityId = entityId,
                Operation = ToValue(operation),
                OccurredAt = occurredAt,
                RecordedAt = utcNow,
                Actor = actor,
                Version = version,
                Before = before,
                After = after,
                SchemaVersion = schemaVersion,
            },
            cancellationToken: cancellationToken
        );
    }

    private async Task<long> AllocateSequence(IClientSessionHandle session, CancellationToken cancellationToken)
    {
        var counter = await dbContext.AuditEventCounters.FindOneAndUpdateAsync(
            session,
            Builders<AuditEventCounter>.Filter.Eq(x => x.Id, "audit_event"),
            Builders<AuditEventCounter>.Update.Inc(x => x.Sequence, 1),
            new FindOneAndUpdateOptions<AuditEventCounter> { IsUpsert = true, ReturnDocument = ReturnDocument.After },
            cancellationToken
        );

        return counter.Sequence;
    }

    private static string ToValue(AuditEventOperation operation) =>
        operation switch
        {
            AuditEventOperation.Insert => "insert",
            AuditEventOperation.Update => "update",
            AuditEventOperation.Delete => "delete",
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
        };
}
