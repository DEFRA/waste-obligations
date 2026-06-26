using Defra.WasteObligations.AuditEvents.Data;
using Defra.WasteObligations.AuditEvents.Entities;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Defra.WasteObligations.AuditEvents;

public class AuditEventDispatchService(
    IAuditEventDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<AuditEventDispatchService> logger
)
{
    public async Task<IReadOnlyCollection<AuditEvent>> ReadUnsent(
        string processName,
        int batchSize,
        CancellationToken cancellationToken
    )
    {
        var filter = Builders<AuditEvent>.Filter.Exists(AuditEventDispatchFieldNames.DispatchPath(processName), false);

        // With secondaryPreferred reads this can see stale dispatch state, so an event may be sent more than once.
        // The topic/queue pipeline is at-least-once delivery, and consumers are expected to handle duplicates.
        return await dbContext
            .AuditEvents.Find(filter)
            .SortBy(x => x.Sequence)
            .Limit(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkDispatched(string processName, AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();

        var filter = Builders<AuditEvent>.Filter.And(
            Builders<AuditEvent>.Filter.Eq(x => x.EventId, auditEvent.EventId),
            Builders<AuditEvent>.Filter.Exists(AuditEventDispatchFieldNames.DispatchPath(processName), false)
        );

        var update = Builders<AuditEvent>.Update.Set(AuditEventDispatchFieldNames.DispatchPath(processName), utcNow);

        var result = await dbContext.AuditEvents.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

        if (result.ModifiedCount == 0)
        {
            logger.LogWarning(
                "Audit event {EventId} was processed but could not be marked as processed by {ProcessName}",
                auditEvent.EventId,
                processName
            );
        }
    }
}
