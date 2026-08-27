using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services;

public class ComplianceDeclarationReviewStateBackfillService(IDbContext dbContext, TimeProvider timeProvider)
    : IComplianceDeclarationReviewStateBackfillService
{
    private const int BatchSize = 500;
    private const string IfNull = "$ifNull";

    public async Task<ComplianceDeclarationReviewStateBackfillResult> Backfill(CancellationToken cancellationToken)
    {
        var snapshot = await ReadSnapshot(cancellationToken);
        if (snapshot?.BackfillCompletedAt is not null)
        {
            return new ComplianceDeclarationReviewStateBackfillResult { AlreadyComplete = true, StateRowCount = 0 };
        }

        return await Reconcile(snapshot, false, cancellationToken);
    }

    // TEMPORARY INITIAL ROLLOUT: Remove after InitialRolloutReconciliationCompletedAt is populated everywhere.
    public async Task<ComplianceDeclarationReviewStateBackfillResult> ReconcileInitialRollout(
        CancellationToken cancellationToken
    )
    {
        var snapshot = await ReadSnapshot(cancellationToken);
        if (snapshot?.InitialRolloutReconciliationCompletedAt is not null)
        {
            return new ComplianceDeclarationReviewStateBackfillResult { AlreadyComplete = true, StateRowCount = 0 };
        }

        return await Reconcile(snapshot, true, cancellationToken);
    }

    private async Task<ComplianceDeclarationReviewStateBackfillResult> Reconcile(
        ComplianceDeclarationReviewStateSnapshot? snapshot,
        bool completeInitialRolloutReconciliation,
        CancellationToken cancellationToken
    )
    {
        var backfillStartedAt = timeProvider.GetUtcNowWithoutMicroseconds();
        var unsubmittedExclusions = await dbContext
            .ComplianceDeclarations.Find(
                Builders<ComplianceDeclaration>.Filter.In(
                    x => x.Status,
                    [ComplianceDeclarationStatus.Submitted, ComplianceDeclarationStatus.Accepted]
                )
            )
            .ToListAsync(cancellationToken);
        var countsByKey = unsubmittedExclusions
            .GroupBy(ComplianceDeclarationReviewStateKey.From)
            .ToDictionary(x => x.Key, x => x.Count());
        var existingNonZeroStates = await dbContext
            .ComplianceDeclarationReviewStates.Find(x => x.UnsubmittedExclusionCount != 0)
            .ToListAsync(cancellationToken);
        var counts = countsByKey
            .Keys.Concat(existingNonZeroStates.Select(ComplianceDeclarationReviewStateKey.From))
            .Distinct()
            .ToDictionary(x => x, x => countsByKey.GetValueOrDefault(x))
            .ToList();

        foreach (var batch in counts.Chunk(BatchSize))
        {
            var writes = batch.Select(x => CreateUpsert(x, backfillStartedAt));
            await dbContext.ComplianceDeclarationReviewStates.BulkWriteAsync(
                writes,
                new BulkWriteOptions { IsOrdered = false },
                cancellationToken
            );
        }

        var completedSnapshot = new ComplianceDeclarationReviewStateSnapshot
        {
            Id = ComplianceDeclarationReviewStateSnapshot.SnapshotId,
            BackfillCompletedAt = snapshot?.BackfillCompletedAt ?? backfillStartedAt,
            InitialRolloutReconciliationCompletedAt = completeInitialRolloutReconciliation
                ? backfillStartedAt
                : snapshot?.InitialRolloutReconciliationCompletedAt,
        };
        await dbContext.ComplianceDeclarationReviewStateSnapshots.ReplaceOneAsync(
            x => x.Id == ComplianceDeclarationReviewStateSnapshot.SnapshotId,
            completedSnapshot,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken
        );

        return new ComplianceDeclarationReviewStateBackfillResult
        {
            AlreadyComplete = false,
            StateRowCount = counts.Count,
        };
    }

    private async Task<ComplianceDeclarationReviewStateSnapshot?> ReadSnapshot(CancellationToken cancellationToken) =>
        await dbContext
            .ComplianceDeclarationReviewStateSnapshots.Find(x =>
                x.Id == ComplianceDeclarationReviewStateSnapshot.SnapshotId
            )
            .SingleOrDefaultAsync(cancellationToken);

    private static WriteModel<ComplianceDeclarationReviewState> CreateUpsert(
        KeyValuePair<ComplianceDeclarationReviewStateKey, int> state,
        DateTime backfillStartedAt
    )
    {
        var filter = Builders<ComplianceDeclarationReviewState>.Filter.And(
            Builders<ComplianceDeclarationReviewState>.Filter.Eq(x => x.OrganisationId, state.Key.OrganisationId),
            Builders<ComplianceDeclarationReviewState>.Filter.Eq(x => x.ObligationYear, state.Key.ObligationYear),
            Builders<ComplianceDeclarationReviewState>.Filter.Eq(x => x.RegistrationType, state.Key.RegistrationType)
        );
        var isOlderThanBackfill = new BsonDocument(
            "$lt",
            new BsonArray
            {
                new BsonDocument(IfNull, new BsonArray { "$updatedAt", DateTime.MinValue }),
                backfillStartedAt,
            }
        );
        var update = new PipelineUpdateDefinition<ComplianceDeclarationReviewState>(
            new[]
            {
                new BsonDocument(
                    "$set",
                    new BsonDocument
                    {
                        ["organisationId"] = new BsonDocument(
                            IfNull,
                            new BsonArray
                            {
                                "$organisationId",
                                new BsonBinaryData(state.Key.OrganisationId, GuidRepresentation.Standard),
                            }
                        ),
                        ["obligationYear"] = new BsonDocument(
                            IfNull,
                            new BsonArray { "$obligationYear", state.Key.ObligationYear }
                        ),
                        ["registrationType"] = new BsonDocument(
                            IfNull,
                            new BsonArray { "$registrationType", state.Key.RegistrationType.ToString() }
                        ),
                        ["unsubmittedExclusionCount"] = new BsonDocument(
                            "$cond",
                            new BsonArray { isOlderThanBackfill, state.Value, "$unsubmittedExclusionCount" }
                        ),
                        ["updatedAt"] = new BsonDocument(
                            "$cond",
                            new BsonArray { isOlderThanBackfill, backfillStartedAt, "$updatedAt" }
                        ),
                    }
                ),
            }
        );

        return new UpdateOneModel<ComplianceDeclarationReviewState>(filter, update) { IsUpsert = true };
    }
}
