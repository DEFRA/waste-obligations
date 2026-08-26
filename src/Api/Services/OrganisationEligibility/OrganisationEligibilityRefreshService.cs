using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services.OrganisationEligibility;

public class OrganisationEligibilityRefreshService(
    IDbContext dbContext,
    IOrganisationEligibilitySource organisationEligibilitySource,
    OrganisationReferenceCacheService organisationReferenceCacheService,
    TimeProvider timeProvider
) : IOrganisationEligibilityRefreshService
{
    public async Task<OrganisationEligibilityRefreshResult> Refresh(CancellationToken cancellationToken)
    {
        var source = await organisationEligibilitySource.Search(cancellationToken);
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var generation = Guid.NewGuid().ToString("N");
        var sourceRows = Mappers.ToEligibilityRows(source.Organisations, generation, utcNow);
        var referenceCaches = await organisationReferenceCacheService.SynchroniseAndResolve(
            sourceRows,
            cancellationToken
        );
        var content = OrganisationEligibilitySnapshotContentBuilder.Create(sourceRows, referenceCaches);
        var activeSnapshot = await dbContext
            .OrganisationEligibilitySnapshots.Find(x => x.Id == OrganisationEligibilitySnapshot.SnapshotId)
            .SingleOrDefaultAsync(cancellationToken);

        if (
            activeSnapshot?.ActiveContentFingerprint == content.Fingerprint
            && activeSnapshot.ActiveRowCount == content.Rows.Count
        )
        {
            await dbContext.OrganisationEligibilitySnapshots.UpdateOneAsync(
                x => x.Id == OrganisationEligibilitySnapshot.SnapshotId,
                Builders<OrganisationEligibilitySnapshot>.Update.Set(x => x.LastVerifiedAt, utcNow),
                cancellationToken: cancellationToken
            );

            return new OrganisationEligibilityRefreshResult
            {
                Outcome = OrganisationEligibilityRefreshOutcome.Unchanged,
                ActiveGeneration = activeSnapshot.ActiveGeneration,
                RowCount = content.Rows.Count,
                ContentFingerprint = content.Fingerprint,
            };
        }

        await dbContext.OrganisationEligibilities.InsertManyAsync(content.Rows, cancellationToken: cancellationToken);
        var writtenRowCount = await dbContext.OrganisationEligibilities.CountDocumentsAsync(
            x => x.Generation == generation,
            cancellationToken: cancellationToken
        );
        if (writtenRowCount != content.Rows.Count)
        {
            throw new InvalidOperationException(
                $"Organisation eligibility generation {generation} wrote {writtenRowCount} rows, expected {content.Rows.Count}"
            );
        }

        var snapshot = new OrganisationEligibilitySnapshot
        {
            Id = OrganisationEligibilitySnapshot.SnapshotId,
            ActiveGeneration = generation,
            ActiveContentFingerprint = content.Fingerprint,
            ActiveRowCount = content.Rows.Count,
            ActiveGenerationPromotedAt = utcNow,
            LastVerifiedAt = utcNow,
        };
        await dbContext.OrganisationEligibilitySnapshots.ReplaceOneAsync(
            x => x.Id == OrganisationEligibilitySnapshot.SnapshotId,
            snapshot,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken
        );

        return new OrganisationEligibilityRefreshResult
        {
            Outcome = OrganisationEligibilityRefreshOutcome.Promoted,
            ActiveGeneration = generation,
            RowCount = content.Rows.Count,
            ContentFingerprint = content.Fingerprint,
        };
    }
}
