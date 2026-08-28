using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services;

public static class OrganisationEligibilitySnapshotState
{
    public static async Task IncrementMaterialisedStateVersion(
        IDbContext dbContext,
        IClientSessionHandle transactionSession,
        CancellationToken cancellationToken
    )
    {
        await dbContext.OrganisationEligibilitySnapshots.UpdateOneAsync(
            transactionSession,
            x => x.Id == OrganisationEligibilitySnapshot.SnapshotId,
            Builders<OrganisationEligibilitySnapshot>
                .Update.SetOnInsert(x => x.Id, OrganisationEligibilitySnapshot.SnapshotId)
                .Inc(x => x.MaterialisedStateVersion, 1),
            new UpdateOptions { IsUpsert = true },
            cancellationToken
        );
    }
}
