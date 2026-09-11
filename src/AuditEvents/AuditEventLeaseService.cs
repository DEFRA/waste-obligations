using Defra.WasteObligations.AuditEvents.Data;
using Defra.WasteObligations.AuditEvents.Entities;
using MongoDB.Driver;

namespace Defra.WasteObligations.AuditEvents;

public class AuditEventLeaseService(IAuditEventDbContext dbContext, TimeProvider timeProvider)
{
    private const string OwnerField = "owner";

    private readonly string _instanceId = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    public async Task<bool> TryAcquire(string processName, TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        var leaseExpiresAt = utcNow.Add(leaseDuration);

        var filter = Builders<AuditEventDispatchLease>.Filter.And(
            Builders<AuditEventDispatchLease>.Filter.Eq(x => x.Id, processName),
            Builders<AuditEventDispatchLease>.Filter.Or(
                Builders<AuditEventDispatchLease>.Filter.Lte(x => x.ExpiresAt, utcNow),
                Builders<AuditEventDispatchLease>.Filter.Eq(x => x.Owner, _instanceId)
            )
        );

        var update = Builders<AuditEventDispatchLease>
            .Update.Set(x => x.Owner, _instanceId)
            .Set(x => x.ExpiresAt, leaseExpiresAt)
            .Set(x => x.UpdatedAt, utcNow)
            .SetOnInsert(x => x.Id, processName)
            .SetOnInsert(x => x.CreatedAt, utcNow);

        try
        {
            await dbContext.AuditEventDispatchLeases.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<AuditEventDispatchLease>
                {
                    IsUpsert = true,
                    ReturnDocument = ReturnDocument.After,
                },
                cancellationToken
            );

            return true;
        }
        catch (MongoCommandException exception) when (exception.Code == 11000)
        {
            return false;
        }
    }

    public async Task<bool> TryRenew(string processName, TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        var leaseExpiresAt = utcNow.Add(leaseDuration);

        var filter = Builders<AuditEventDispatchLease>.Filter.And(
            Builders<AuditEventDispatchLease>.Filter.Eq(x => x.Id, processName),
            Builders<AuditEventDispatchLease>.Filter.Eq(x => x.Owner, _instanceId)
        );

        var update = Builders<AuditEventDispatchLease>
            .Update.Set(x => x.ExpiresAt, leaseExpiresAt)
            .Set(x => x.UpdatedAt, utcNow);

        var result = await dbContext.AuditEventDispatchLeases.UpdateOneAsync(
            filter,
            update,
            cancellationToken: cancellationToken
        );

        return result.MatchedCount == 1;
    }

    public async Task Release(string processName, CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();

        var filter = Builders<AuditEventDispatchLease>.Filter.And(
            Builders<AuditEventDispatchLease>.Filter.Eq(x => x.Id, processName),
            Builders<AuditEventDispatchLease>.Filter.Eq(x => x.Owner, _instanceId)
        );

        var update = Builders<AuditEventDispatchLease>
            .Update.Set(x => x.ExpiresAt, utcNow)
            .Set(x => x.UpdatedAt, utcNow)
            .Set(x => x.LastSentAt, utcNow)
            .Unset(OwnerField);

        await dbContext.AuditEventDispatchLeases.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }
}
