using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(nameof(OrganisationComplianceDeclarationEligibility), MigrationDirection.Both)]
public class OrganisationEligibilityObligationMetricSorting : MongoMigration
{
    private const string ObligationCoveragePercentageField = "obligationCoveragePercentage";
    private const string ObligationYearField = "obligationYear";
    private const string OrganisationIdField = "organisationId";
    private const string PercentageMetIndexName =
        "Generation_ObligationYear_RegistrationType_IsVisibleInUnsubmittedView_ObligationCoveragePercentage_Name_OrganisationId";
    private const string RecyclingObligationsIndexName =
        "Generation_ObligationYear_RegistrationType_IsVisibleInUnsubmittedView_RecyclingObligationsMet_Name_OrganisationId";
    private const string RecyclingObligationsMetField = "recyclingObligationsMet";
    private const string ReferenceNumberIndexName =
        "Generation_ObligationYear_RegistrationType_IsVisibleInUnsubmittedView_ReferenceNumber_Name_OrganisationId";

    public override MigrationVersion Version => new(1, 0, 11);

    public override string Name => "012 - Organisation eligibility obligation metric sorting";

    public override async Task UpAsync(MigrationContext context)
    {
        await BackfillMetrics(context);
        await CreateIndex(
            context,
            ReferenceNumberIndexName,
            Builders<OrganisationComplianceDeclarationEligibility>
                .IndexKeys.Ascending(x => x.Generation)
                .Ascending(x => x.ObligationYear)
                .Ascending(x => x.RegistrationType)
                .Ascending(x => x.IsVisibleInUnsubmittedView)
                .Ascending(x => x.ReferenceNumber)
                .Ascending(x => x.Name)
                .Ascending(x => x.OrganisationId)
        );
        await CreateIndex(
            context,
            RecyclingObligationsIndexName,
            Builders<OrganisationComplianceDeclarationEligibility>
                .IndexKeys.Ascending(x => x.Generation)
                .Ascending(x => x.ObligationYear)
                .Ascending(x => x.RegistrationType)
                .Ascending(x => x.IsVisibleInUnsubmittedView)
                .Ascending(x => x.RecyclingObligationsMet)
                .Ascending(x => x.Name)
                .Ascending(x => x.OrganisationId)
        );
        await CreateIndex(
            context,
            PercentageMetIndexName,
            Builders<OrganisationComplianceDeclarationEligibility>
                .IndexKeys.Ascending(x => x.Generation)
                .Ascending(x => x.ObligationYear)
                .Ascending(x => x.RegistrationType)
                .Ascending(x => x.IsVisibleInUnsubmittedView)
                .Ascending(x => x.ObligationCoveragePercentage)
                .Ascending(x => x.Name)
                .Ascending(x => x.OrganisationId)
        );
    }

    public override async Task DownAsync(MigrationContext context)
    {
        await DropIndex<OrganisationComplianceDeclarationEligibility>(context, ReferenceNumberIndexName);
        await DropIndex<OrganisationComplianceDeclarationEligibility>(context, RecyclingObligationsIndexName);
        await DropIndex<OrganisationComplianceDeclarationEligibility>(context, PercentageMetIndexName);

        var eligibility = context.Database.GetCollection<BsonDocument>(
            nameof(OrganisationComplianceDeclarationEligibility)
        );
        await eligibility.UpdateManyAsync(
            Builders<BsonDocument>.Filter.Empty,
            Builders<BsonDocument>.Update.Unset(RecyclingObligationsMetField).Unset(ObligationCoveragePercentageField),
            cancellationToken: context.CancellationToken
        );
    }

    private static async Task BackfillMetrics(MigrationContext context)
    {
        var eligibility = context.Database.GetCollection<BsonDocument>(
            nameof(OrganisationComplianceDeclarationEligibility)
        );
        var summaries = context.Database.GetCollection<BsonDocument>(nameof(OrganisationObligationSummary));
        var filter = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Exists(RecyclingObligationsMetField, false),
            Builders<BsonDocument>.Filter.Exists(ObligationCoveragePercentageField, false)
        );

        using var cursor = await eligibility.Find(filter).ToCursorAsync(context.CancellationToken);
        while (await cursor.MoveNextAsync(context.CancellationToken))
        {
            foreach (var row in cursor.Current)
            {
                var summary = await summaries
                    .Find(
                        Builders<BsonDocument>.Filter.And(
                            Builders<BsonDocument>.Filter.Eq(OrganisationIdField, row[OrganisationIdField]),
                            Builders<BsonDocument>.Filter.Eq(ObligationYearField, row[ObligationYearField])
                        )
                    )
                    .FirstOrDefaultAsync(context.CancellationToken);
                await SetIfMissing(
                    eligibility,
                    row,
                    RecyclingObligationsMetField,
                    summary?.GetValue(RecyclingObligationsMetField, BsonNull.Value) ?? BsonNull.Value,
                    context.CancellationToken
                );
                await SetIfMissing(
                    eligibility,
                    row,
                    ObligationCoveragePercentageField,
                    summary?.GetValue(ObligationCoveragePercentageField, new BsonDecimal128(0))
                        ?? new BsonDecimal128(0),
                    context.CancellationToken
                );
            }
        }
    }

    private static async Task SetIfMissing(
        IMongoCollection<BsonDocument> collection,
        BsonDocument row,
        string field,
        BsonValue value,
        CancellationToken cancellationToken
    )
    {
        if (row.Contains(field))
            return;

        await collection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("_id", row["_id"]),
                Builders<BsonDocument>.Filter.Exists(field, false)
            ),
            Builders<BsonDocument>.Update.Set(field, value),
            cancellationToken: cancellationToken
        );
    }
}
