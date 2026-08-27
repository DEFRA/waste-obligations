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
    private const int DuplicateKeyErrorCode = 11000;
    private const string ActiveGenerationChangedMessage =
        "The active organisation eligibility generation changed during refresh";

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
            activeSnapshot?.ActiveGeneration is null
            && referenceCaches.Any(x => x.ResolutionState == OrganisationReferenceNumberResolutionState.Failed)
        )
        {
            throw new InvalidOperationException(
                "Initial organisation eligibility generation contains failed Account reference lookups"
            );
        }

        if (
            activeSnapshot?.ActiveContentFingerprint == content.Fingerprint
            && activeSnapshot.ActiveRowCount == content.Rows.Count
        )
        {
            await VerifyActiveGeneration(activeSnapshot, utcNow, cancellationToken);

            return new OrganisationEligibilityRefreshResult
            {
                Outcome = OrganisationEligibilityRefreshOutcome.Unchanged,
                ActiveGeneration = activeSnapshot.ActiveGeneration,
                RowCount = content.Rows.Count,
                ContentFingerprint = content.Fingerprint,
            };
        }

        await dbContext.OrganisationComplianceDeclarationEligibilities.InsertManyAsync(
            content.Rows,
            cancellationToken: cancellationToken
        );
        var writtenRowCount = await dbContext.OrganisationComplianceDeclarationEligibilities.CountDocumentsAsync(
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
        await PromoteActiveGeneration(activeSnapshot, snapshot, cancellationToken);

        return new OrganisationEligibilityRefreshResult
        {
            Outcome = OrganisationEligibilityRefreshOutcome.Promoted,
            ActiveGeneration = generation,
            RowCount = content.Rows.Count,
            ContentFingerprint = content.Fingerprint,
        };
    }

    private async Task VerifyActiveGeneration(
        OrganisationEligibilitySnapshot activeSnapshot,
        DateTime utcNow,
        CancellationToken cancellationToken
    )
    {
        var result = await dbContext.OrganisationEligibilitySnapshots.UpdateOneAsync(
            ActiveGenerationFilter(activeSnapshot.ActiveGeneration),
            Builders<OrganisationEligibilitySnapshot>.Update.Set(x => x.LastVerifiedAt, utcNow),
            cancellationToken: cancellationToken
        );

        EnsureActiveGenerationUnchanged(result.MatchedCount);
    }

    private async Task PromoteActiveGeneration(
        OrganisationEligibilitySnapshot? activeSnapshot,
        OrganisationEligibilitySnapshot replacement,
        CancellationToken cancellationToken
    )
    {
        if (activeSnapshot is null)
        {
            try
            {
                await dbContext.OrganisationEligibilitySnapshots.InsertOneAsync(
                    replacement,
                    cancellationToken: cancellationToken
                );
            }
            catch (MongoCommandException exception) when (exception.Code == DuplicateKeyErrorCode)
            {
                throw new InvalidOperationException(ActiveGenerationChangedMessage, exception);
            }
            catch (MongoWriteException exception) when (exception.WriteError.Code == DuplicateKeyErrorCode)
            {
                throw new InvalidOperationException(ActiveGenerationChangedMessage, exception);
            }

            return;
        }

        var result = await dbContext.OrganisationEligibilitySnapshots.ReplaceOneAsync(
            ActiveGenerationFilter(activeSnapshot.ActiveGeneration),
            replacement,
            cancellationToken: cancellationToken
        );

        EnsureActiveGenerationUnchanged(result.MatchedCount);
    }

    private static FilterDefinition<OrganisationEligibilitySnapshot> ActiveGenerationFilter(string? activeGeneration) =>
        Builders<OrganisationEligibilitySnapshot>.Filter.And(
            Builders<OrganisationEligibilitySnapshot>.Filter.Eq(x => x.Id, OrganisationEligibilitySnapshot.SnapshotId),
            Builders<OrganisationEligibilitySnapshot>.Filter.Eq(x => x.ActiveGeneration, activeGeneration)
        );

    private static void EnsureActiveGenerationUnchanged(long matchedCount)
    {
        if (matchedCount != 1)
        {
            throw new InvalidOperationException(ActiveGenerationChangedMessage);
        }
    }
}
