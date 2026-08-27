using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services.OrganisationObligations;

public class OrganisationObligationHydrationLeaseService(
    IMongoDatabase database,
    TimeProvider timeProvider,
    ILogger<OrganisationObligationHydrationLeaseService> logger
) : IOrganisationObligationHydrationLeaseService
{
    private const string OwnerField = "owner";

    private readonly string _instanceId = $"{Environment.MachineName}-{Guid.NewGuid():N}";
    private readonly IMongoCollection<OrganisationObligationHydrationLease> _leases =
        database.GetCollection<OrganisationObligationHydrationLease>(
            OrganisationObligationHydrationLease.CollectionName
        );

    public async Task<bool> TryAcquire(TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        var leaseExpiresAt = utcNow.Add(leaseDuration);

        var filter = Builders<OrganisationObligationHydrationLease>.Filter.And(
            Builders<OrganisationObligationHydrationLease>.Filter.Eq(
                x => x.Id,
                OrganisationObligationHydrationLease.LeaseId
            ),
            Builders<OrganisationObligationHydrationLease>.Filter.Or(
                Builders<OrganisationObligationHydrationLease>.Filter.Lte(x => x.ExpiresAt, utcNow),
                Builders<OrganisationObligationHydrationLease>.Filter.Eq(x => x.Owner, _instanceId)
            )
        );

        var update = Builders<OrganisationObligationHydrationLease>
            .Update.Set(x => x.Owner, _instanceId)
            .Set(x => x.ExpiresAt, leaseExpiresAt)
            .Set(x => x.UpdatedAt, utcNow)
            .SetOnInsert(x => x.Id, OrganisationObligationHydrationLease.LeaseId)
            .SetOnInsert(x => x.CreatedAt, utcNow);

        try
        {
            await _leases.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<OrganisationObligationHydrationLease>
                {
                    IsUpsert = true,
                    ReturnDocument = ReturnDocument.After,
                },
                cancellationToken
            );

            logger.LogInformation("Acquired organisation obligation hydration lease by {InstanceId}", _instanceId);

            return true;
        }
        catch (MongoCommandException exception) when (exception.Code == 11000)
        {
            logger.LogInformation(
                exception,
                "Organisation obligation hydration lease is already acquired by another instance. Current instance {InstanceId} did not acquire it",
                _instanceId
            );

            return false;
        }
    }

    public async Task<bool> TryRenew(TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        var leaseExpiresAt = utcNow.Add(leaseDuration);

        var filter = Builders<OrganisationObligationHydrationLease>.Filter.And(
            Builders<OrganisationObligationHydrationLease>.Filter.Eq(
                x => x.Id,
                OrganisationObligationHydrationLease.LeaseId
            ),
            Builders<OrganisationObligationHydrationLease>.Filter.Eq(x => x.Owner, _instanceId)
        );

        var update = Builders<OrganisationObligationHydrationLease>
            .Update.Set(x => x.ExpiresAt, leaseExpiresAt)
            .Set(x => x.UpdatedAt, utcNow);

        var result = await _leases.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

        if (result.MatchedCount != 1)
            return false;

        logger.LogInformation("Renewed organisation obligation hydration lease by {InstanceId}", _instanceId);

        return true;
    }

    public async Task Release(CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();

        var filter = Builders<OrganisationObligationHydrationLease>.Filter.And(
            Builders<OrganisationObligationHydrationLease>.Filter.Eq(
                x => x.Id,
                OrganisationObligationHydrationLease.LeaseId
            ),
            Builders<OrganisationObligationHydrationLease>.Filter.Eq(x => x.Owner, _instanceId)
        );

        var update = Builders<OrganisationObligationHydrationLease>
            .Update.Set(x => x.ExpiresAt, utcNow)
            .Set(x => x.UpdatedAt, utcNow)
            .Set(x => x.LastReleasedAt, utcNow)
            .Unset(OwnerField);

        var result = await _leases.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

        if (result.ModifiedCount == 1)
        {
            logger.LogInformation("Released organisation obligation hydration lease by {InstanceId}", _instanceId);
        }
    }
}
