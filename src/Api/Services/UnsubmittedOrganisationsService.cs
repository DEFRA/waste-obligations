using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.OrganisationEligibility;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using OrganisationComplianceDeclarationEligibilityEntity = Defra.WasteObligations.Api.Data.Entities.OrganisationComplianceDeclarationEligibility;

namespace Defra.WasteObligations.Api.Services;

public class UnsubmittedOrganisationsService(
    IDbContext dbContext,
    IOptions<OrganisationEligibilityOptions> options,
    TimeProvider timeProvider,
    ILogger<UnsubmittedOrganisationsService> logger
) : IUnsubmittedOrganisationsService
{
    public async Task<UnsubmittedOrganisationSearchResult> Search(
        int obligationYear,
        RegistrationType registrationType,
        IReadOnlyCollection<ComplianceDeclarationSort> sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        var snapshot = await dbContext
            .OrganisationEligibilitySnapshots.Find(x => x.Id == OrganisationEligibilitySnapshot.SnapshotId)
            .SingleOrDefaultAsync(cancellationToken);
        if (snapshot?.ActiveGeneration is not { } activeGeneration)
        {
            logger.LogError("Unsubmitted organisation query has no active organisation generation");

            return EmptyResult();
        }

        var reviewStateSnapshot = await dbContext
            .ComplianceDeclarationReviewStateSnapshots.Find(x =>
                x.Id == ComplianceDeclarationReviewStateSnapshot.SnapshotId
            )
            .SingleOrDefaultAsync(cancellationToken);
        if (reviewStateSnapshot?.BackfillCompletedAt is null)
        {
            logger.LogError("Unsubmitted organisation query has no completed declaration review state backfill");

            return EmptyResult();
        }

        if (
            snapshot.LastVerifiedAt is null
            || timeProvider.GetUtcNow().UtcDateTime - snapshot.LastVerifiedAt.Value
                > options.Value.MaximumAllowedStaleness
        )
        {
            logger.LogError(
                "Unsubmitted organisation query is using an organisation generation last verified at {LastVerifiedAt}",
                snapshot.LastVerifiedAt
            );
        }

        var eligible = Builders<OrganisationComplianceDeclarationEligibilityEntity>.Filter.And(
            Builders<OrganisationComplianceDeclarationEligibilityEntity>.Filter.Eq(x => x.Generation, activeGeneration),
            Builders<OrganisationComplianceDeclarationEligibilityEntity>.Filter.Eq(
                x => x.ObligationYear,
                obligationYear
            ),
            Builders<OrganisationComplianceDeclarationEligibilityEntity>.Filter.Eq(
                x => x.RegistrationType,
                registrationType
            ),
            Builders<OrganisationComplianceDeclarationEligibilityEntity>.Filter.Eq(
                x => x.RegistrationStatus,
                OrganisationRegistrationStatus.Registered
            ),
            Builders<OrganisationComplianceDeclarationEligibilityEntity>.Filter.Eq(
                x => x.ReferenceNumberResolutionState,
                OrganisationReferenceNumberResolutionState.Resolved
            )
        );
        var sortDefinition = BuildSort(sort);
        var result = await dbContext
            .OrganisationComplianceDeclarationEligibilities.Aggregate()
            .Match(eligible)
            .AppendStage<BsonDocument>(ReviewStateLookup())
            .AppendStage<BsonDocument>(UnsubmittedMatch())
            .AppendStage<BsonDocument>(Project())
            .AppendStage<BsonDocument>(Facet(sortDefinition, page, pageSize))
            .SingleOrDefaultAsync(cancellationToken);
        var rows = result is null ? [] : ReadRows(result);
        var total = result is null ? 0 : ReadTotal(result);

        return new UnsubmittedOrganisationSearchResult { Rows = rows, Total = total };
    }

    private static BsonDocument ReviewStateLookup() =>
        new(
            "$lookup",
            new BsonDocument
            {
                ["from"] = nameof(ComplianceDeclarationReviewState),
                ["let"] = new BsonDocument
                {
                    ["organisationId"] = "$organisationId",
                    ["obligationYear"] = "$obligationYear",
                    ["registrationType"] = "$registrationType",
                },
                ["pipeline"] = new BsonArray
                {
                    new BsonDocument(
                        "$match",
                        new BsonDocument(
                            "$expr",
                            new BsonDocument(
                                "$and",
                                new BsonArray
                                {
                                    new BsonDocument("$eq", new BsonArray { "$organisationId", "$$organisationId" }),
                                    new BsonDocument("$eq", new BsonArray { "$obligationYear", "$$obligationYear" }),
                                    new BsonDocument(
                                        "$eq",
                                        new BsonArray { "$registrationType", "$$registrationType" }
                                    ),
                                }
                            )
                        )
                    ),
                    new BsonDocument("$limit", 1),
                },
                ["as"] = "reviewState",
            }
        );

    private static BsonDocument UnsubmittedMatch() =>
        new(
            "$match",
            new BsonDocument(
                "$or",
                new BsonArray
                {
                    new BsonDocument("reviewState", new BsonDocument("$eq", new BsonArray())),
                    new BsonDocument("reviewState.unsubmittedExclusionCount", 0),
                }
            )
        );

    private static BsonDocument Project() =>
        new(
            "$project",
            new BsonDocument
            {
                ["_id"] = 0,
                ["organisationId"] = 1,
                ["obligationYear"] = 1,
                ["registrationType"] = 1,
                ["name"] = 1,
                ["referenceNumber"] = 1,
            }
        );

    private static BsonDocument Facet(BsonDocument sort, int page, int pageSize) =>
        new(
            "$facet",
            new BsonDocument
            {
                ["rows"] = new BsonArray
                {
                    new BsonDocument("$sort", sort),
                    new BsonDocument("$skip", (long)(page - 1) * pageSize),
                    new BsonDocument("$limit", pageSize),
                },
                ["total"] = new BsonArray { new BsonDocument("$count", "value") },
            }
        );

    private static BsonDocument BuildSort(IReadOnlyCollection<ComplianceDeclarationSort> sort)
    {
        var direction = sort.SingleOrDefault()?.Direction ?? ComplianceDeclarationSortDirection.Ascending;

        return new BsonDocument
        {
            ["name"] = direction is ComplianceDeclarationSortDirection.Ascending ? 1 : -1,
            ["organisationId"] = 1,
        };
    }

    private static List<UnsubmittedOrganisationSearchRow> ReadRows(BsonDocument document) =>
        document["rows"]
            .AsBsonArray.Select(x => BsonSerializer.Deserialize<UnsubmittedOrganisationSearchRow>(x.AsBsonDocument))
            .ToList();

    private static UnsubmittedOrganisationSearchResult EmptyResult() => new() { Rows = [], Total = 0 };

    private static int ReadTotal(BsonDocument document)
    {
        var total = document["total"].AsBsonArray;

        return total.Count == 0 ? 0 : total[0].AsBsonDocument["value"].ToInt32();
    }
}
