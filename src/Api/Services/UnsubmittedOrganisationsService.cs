using System.Text.RegularExpressions;
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
        string? search,
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
                x => x.IsVisibleInUnsubmittedView,
                true
            )
        );
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(search.Trim()), "i");
            eligible &= Builders<OrganisationComplianceDeclarationEligibilityEntity>.Filter.Or(
                Builders<OrganisationComplianceDeclarationEligibilityEntity>.Filter.Regex(x => x.Name, pattern),
                Builders<OrganisationComplianceDeclarationEligibilityEntity>.Filter.Regex(x => x.TradingName, pattern),
                Builders<OrganisationComplianceDeclarationEligibilityEntity>.Filter.Regex(
                    x => x.ReferenceNumber,
                    pattern
                )
            );
        }

        var sortDefinition = BuildSort(sort);
        var result = await dbContext
            .OrganisationComplianceDeclarationEligibilities.Aggregate()
            .Match(eligible)
            .AppendStage<BsonDocument>(Project())
            .AppendStage<BsonDocument>(Facet(sortDefinition, page, pageSize))
            .SingleOrDefaultAsync(cancellationToken);
        var rows = result is null ? [] : await EnrichRows(ReadRows(result), obligationYear, cancellationToken);
        var total = result is null ? 0 : ReadTotal(result);

        return new UnsubmittedOrganisationSearchResult { Rows = rows, Total = total };
    }

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

    private async Task<List<UnsubmittedOrganisationSearchRow>> EnrichRows(
        List<UnsubmittedOrganisationSearchRow> rows,
        int obligationYear,
        CancellationToken cancellationToken
    )
    {
        if (rows.Count == 0)
            return rows;

        var summaries = await dbContext
            .OrganisationObligationSummaries.Find(x =>
                x.ObligationYear == obligationYear && rows.Select(row => row.OrganisationId).Contains(x.OrganisationId)
            )
            .ToListAsync(cancellationToken);
        var summariesByOrganisationId = summaries.ToDictionary(x => x.OrganisationId);
        return rows.Select(row =>
            {
                if (!summariesByOrganisationId.TryGetValue(row.OrganisationId, out var summary))
                {
                    return row with { ObligationCoveragePercentage = 0 };
                }

                return row with
                {
                    RecyclingObligationsMet = summary.RecyclingObligationsMet,
                    ObligationCoveragePercentage = summary.ObligationCoveragePercentage ?? 0,
                };
            })
            .ToList();
    }

    private static UnsubmittedOrganisationSearchResult EmptyResult() => new() { Rows = [], Total = 0 };

    private static int ReadTotal(BsonDocument document)
    {
        var total = document["total"].AsBsonArray;

        return total.Count == 0 ? 0 : total[0].AsBsonDocument["value"].ToInt32();
    }
}
