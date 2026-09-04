using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services;

internal sealed class BackgroundWorkerLeaseService(
    IMongoDatabase database,
    TimeProvider timeProvider,
    ILogger logger,
    string collectionName,
    string leaseId,
    string workerName
)
{
    private const string OwnerField = "owner";

    private readonly string _instanceId = $"{Environment.MachineName}-{Guid.NewGuid():N}";
    private readonly IMongoCollection<BackgroundWorkerLease> _leases = database.GetCollection<BackgroundWorkerLease>(
        collectionName
    );

    public async Task<bool> TryAcquire(TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        var leaseExpiresAt = utcNow.Add(leaseDuration);

        var filter = Builders<BackgroundWorkerLease>.Filter.And(
            Builders<BackgroundWorkerLease>.Filter.Eq(x => x.Id, leaseId),
            Builders<BackgroundWorkerLease>.Filter.Or(
                Builders<BackgroundWorkerLease>.Filter.Lte(x => x.ExpiresAt, utcNow),
                Builders<BackgroundWorkerLease>.Filter.Eq(x => x.Owner, _instanceId)
            )
        );

        var update = Builders<BackgroundWorkerLease>
            .Update.Set(x => x.Owner, _instanceId)
            .Set(x => x.ExpiresAt, leaseExpiresAt)
            .Set(x => x.UpdatedAt, utcNow)
            .SetOnInsert(x => x.Id, leaseId)
            .SetOnInsert(x => x.CreatedAt, utcNow);

        try
        {
            await _leases.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<BackgroundWorkerLease>
                {
                    IsUpsert = true,
                    ReturnDocument = ReturnDocument.After,
                },
                cancellationToken
            );

            logger.LogInformation("Acquired {WorkerName} lease by {InstanceId}", workerName, _instanceId);

            return true;
        }
        catch (MongoCommandException exception) when (exception.Code == 11000)
        {
            logger.LogInformation(
                exception,
                "{WorkerName} lease is already acquired by another instance. Current instance {InstanceId} did not acquire it",
                workerName,
                _instanceId
            );

            return false;
        }
    }

    public async Task<bool> TryRenew(TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        var leaseExpiresAt = utcNow.Add(leaseDuration);

        var filter = Builders<BackgroundWorkerLease>.Filter.And(
            Builders<BackgroundWorkerLease>.Filter.Eq(x => x.Id, leaseId),
            Builders<BackgroundWorkerLease>.Filter.Eq(x => x.Owner, _instanceId)
        );

        var update = Builders<BackgroundWorkerLease>
            .Update.Set(x => x.ExpiresAt, leaseExpiresAt)
            .Set(x => x.UpdatedAt, utcNow);

        var result = await _leases.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

        if (result.MatchedCount != 1)
            return false;

        logger.LogInformation("Renewed {WorkerName} lease by {InstanceId}", workerName, _instanceId);

        return true;
    }

    public async Task Release(CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();

        var filter = Builders<BackgroundWorkerLease>.Filter.And(
            Builders<BackgroundWorkerLease>.Filter.Eq(x => x.Id, leaseId),
            Builders<BackgroundWorkerLease>.Filter.Eq(x => x.Owner, _instanceId)
        );

        var update = Builders<BackgroundWorkerLease>
            .Update.Set(x => x.ExpiresAt, utcNow)
            .Set(x => x.UpdatedAt, utcNow)
            .Set(x => x.LastReleasedAt, utcNow)
            .Unset(OwnerField);

        var result = await _leases.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

        if (result.ModifiedCount == 1)
        {
            logger.LogInformation("Released {WorkerName} lease by {InstanceId}", workerName, _instanceId);
        }
    }
}
