using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.OrganisationEligibility;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using OrganisationEligibilityEntity = Defra.WasteObligations.Api.Data.Entities.OrganisationEligibility;

namespace Defra.WasteObligations.Api.Services;

public interface IUnsubmittedComplianceDeclarationsService
{
    Task<UnsubmittedComplianceDeclarationsSearchResult> Search(
        int obligationYear,
        RegistrationType registrationType,
        IReadOnlyCollection<ComplianceDeclarationSort> sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    );
}

public class UnsubmittedComplianceDeclarationsService(
    IDbContext dbContext,
    IOptions<OrganisationEligibilityOptions> options,
    TimeProvider timeProvider
) : IUnsubmittedComplianceDeclarationsService
{
    public async Task<UnsubmittedComplianceDeclarationsSearchResult> Search(
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
        if (
            snapshot?.ActiveGeneration is null
            || snapshot.LastVerifiedAt is null
            || timeProvider.GetUtcNow().UtcDateTime - snapshot.LastVerifiedAt > options.Value.MaximumAllowedStaleness
        )
        {
            throw new UnsubmittedComplianceDeclarationsUnavailableException(
                "Organisation eligibility data is unavailable or stale"
            );
        }

        var reviewStateSnapshot = await dbContext
            .ComplianceDeclarationReviewStateSnapshots.Find(x =>
                x.Id == ComplianceDeclarationReviewStateSnapshot.SnapshotId
            )
            .SingleOrDefaultAsync(cancellationToken);
        if (reviewStateSnapshot?.BackfillCompletedAt is null)
        {
            throw new UnsubmittedComplianceDeclarationsUnavailableException(
                "Compliance declaration review state has not been backfilled"
            );
        }

        var eligible = Builders<OrganisationEligibilityEntity>.Filter.And(
            Builders<OrganisationEligibilityEntity>.Filter.Eq(x => x.Generation, snapshot.ActiveGeneration),
            Builders<OrganisationEligibilityEntity>.Filter.Eq(x => x.ObligationYear, obligationYear),
            Builders<OrganisationEligibilityEntity>.Filter.Eq(x => x.RegistrationType, registrationType),
            Builders<OrganisationEligibilityEntity>.Filter.Eq(
                x => x.RegistrationStatus,
                OrganisationRegistrationStatus.Registered
            ),
            Builders<OrganisationEligibilityEntity>.Filter.Eq(
                x => x.ReferenceNumberResolutionState,
                OrganisationReferenceNumberResolutionState.Resolved
            ),
            Builders<OrganisationEligibilityEntity>.Filter.Ne(x => x.ReferenceNumber, null),
            Builders<OrganisationEligibilityEntity>.Filter.Ne(x => x.ReferenceNumber, "")
        );
        var sortDefinition = BuildSort(sort);
        var result = await dbContext
            .OrganisationEligibilities.Aggregate()
            .Match(eligible)
            .AppendStage<BsonDocument>(ReviewStateLookup())
            .AppendStage<BsonDocument>(UnsubmittedMatch())
            .AppendStage<BsonDocument>(Project())
            .AppendStage<BsonDocument>(Facet(sortDefinition, page, pageSize))
            .SingleOrDefaultAsync(cancellationToken);
        var rows = result is null ? [] : ReadRows(result);
        var total = result is null ? 0 : ReadTotal(result);

        return new UnsubmittedComplianceDeclarationsSearchResult
        {
            Rows = rows,
            Total = total,
            EligibilityAsOf = snapshot.LastVerifiedAt.Value,
        };
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

    private static IReadOnlyList<UnsubmittedComplianceDeclarationSearchRow> ReadRows(BsonDocument document) =>
        document["rows"]
            .AsBsonArray.Select(x =>
                BsonSerializer.Deserialize<UnsubmittedComplianceDeclarationSearchRow>(x.AsBsonDocument)
            )
            .ToList();

    private static int ReadTotal(BsonDocument document)
    {
        var total = document["total"].AsBsonArray;

        return total.Count == 0 ? 0 : total[0].AsBsonDocument["value"].ToInt32();
    }
}

public record UnsubmittedComplianceDeclarationsSearchResult
{
    public required IReadOnlyList<UnsubmittedComplianceDeclarationSearchRow> Rows { get; init; }
    public required int Total { get; init; }
    public required DateTime EligibilityAsOf { get; init; }
}

[BsonIgnoreExtraElements]
public record UnsubmittedComplianceDeclarationSearchRow
{
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid OrganisationId { get; init; }

    public int ObligationYear { get; init; }
    public RegistrationType RegistrationType { get; init; }
    public required string Name { get; init; }
    public required string ReferenceNumber { get; init; }
}

public class UnsubmittedComplianceDeclarationsUnavailableException(string message) : Exception(message);
