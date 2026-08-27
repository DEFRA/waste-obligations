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
        var snapshot = await dbContext
            .ComplianceDeclarationReviewStateSnapshots.Find(x =>
                x.Id == ComplianceDeclarationReviewStateSnapshot.SnapshotId
            )
            .SingleOrDefaultAsync(cancellationToken);
        if (snapshot?.BackfillCompletedAt is not null)
        {
            return new ComplianceDeclarationReviewStateBackfillResult { AlreadyComplete = true, StateRowCount = 0 };
        }

        var backfillStartedAt = timeProvider.GetUtcNowWithoutMicroseconds();
        var unsubmittedExclusions = await dbContext
            .ComplianceDeclarations.Find(
                Builders<ComplianceDeclaration>.Filter.In(
                    x => x.Status,
                    [ComplianceDeclarationStatus.Submitted, ComplianceDeclarationStatus.Accepted]
                )
            )
            .ToListAsync(cancellationToken);
        var counts = unsubmittedExclusions
            .GroupBy(ReviewStateKey.From)
            .Select(x => new ReviewStateCount(x.Key, x.Count()))
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

        await dbContext.ComplianceDeclarationReviewStateSnapshots.ReplaceOneAsync(
            x => x.Id == ComplianceDeclarationReviewStateSnapshot.SnapshotId,
            new ComplianceDeclarationReviewStateSnapshot
            {
                Id = ComplianceDeclarationReviewStateSnapshot.SnapshotId,
                BackfillCompletedAt = backfillStartedAt,
            },
            new ReplaceOptions { IsUpsert = true },
            cancellationToken
        );

        return new ComplianceDeclarationReviewStateBackfillResult
        {
            AlreadyComplete = false,
            StateRowCount = counts.Count,
        };
    }

    private static WriteModel<ComplianceDeclarationReviewState> CreateUpsert(
        ReviewStateCount state,
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
                            new BsonArray { isOlderThanBackfill, state.Count, "$unsubmittedExclusionCount" }
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

    private sealed record ReviewStateKey(Guid OrganisationId, int ObligationYear, RegistrationType RegistrationType)
    {
        public static ReviewStateKey From(ComplianceDeclaration declaration) =>
            new(declaration.Organisation.Id, declaration.ObligationYear, declaration.Organisation.RegistrationType);
    }

    private sealed record ReviewStateCount(ReviewStateKey Key, int Count);
}
