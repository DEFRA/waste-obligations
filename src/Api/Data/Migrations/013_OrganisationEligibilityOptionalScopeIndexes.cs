using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(nameof(OrganisationComplianceDeclarationEligibility), MigrationDirection.Both)]
public class OrganisationEligibilityOptionalScopeIndexes : MongoMigration
{
    private const string CurrentNameIndexName =
        "Generation_ObligationYear_RegistrationType_IsVisibleInUnsubmittedView_Name_OrganisationId";
    private const string CurrentPercentageMetIndexName =
        "Generation_ObligationYear_RegistrationType_IsVisibleInUnsubmittedView_ObligationCoveragePercentage_Name_OrganisationId";
    private const string CurrentRecyclingObligationsIndexName =
        "Generation_ObligationYear_RegistrationType_IsVisibleInUnsubmittedView_RecyclingObligationsMet_Name_OrganisationId";
    private const string CurrentReferenceNumberIndexName =
        "Generation_ObligationYear_RegistrationType_IsVisibleInUnsubmittedView_ReferenceNumber_Name_OrganisationId";
    private const string NameIndexName = "Generation_IsVisibleInUnsubmittedView_Name_OrganisationId";
    private const string PercentageMetIndexName =
        "Generation_IsVisibleInUnsubmittedView_ObligationCoveragePercentage_Name_OrganisationId";
    private const string RecyclingObligationsIndexName =
        "Generation_IsVisibleInUnsubmittedView_RecyclingObligationsMet_Name_OrganisationId";
    private const string ReferenceNumberIndexName =
        "Generation_IsVisibleInUnsubmittedView_ReferenceNumber_Name_OrganisationId";

    public override MigrationVersion Version => new(1, 0, 12);

    public override string Name => "013 - Organisation eligibility optional scope indexes";

    public override async Task UpAsync(MigrationContext context)
    {
        await DropCurrentIndexes(context);
        await CreateOptionalScopeIndexes(context);
    }

    public override async Task DownAsync(MigrationContext context)
    {
        await DropOptionalScopeIndexes(context);
        await CreateCurrentIndexes(context);
    }

    private static async Task DropCurrentIndexes(MigrationContext context)
    {
        await DropIndexes(
            context,
            CurrentNameIndexName,
            CurrentReferenceNumberIndexName,
            CurrentRecyclingObligationsIndexName,
            CurrentPercentageMetIndexName
        );
    }

    private static async Task DropOptionalScopeIndexes(MigrationContext context)
    {
        await DropIndexes(
            context,
            NameIndexName,
            ReferenceNumberIndexName,
            RecyclingObligationsIndexName,
            PercentageMetIndexName
        );
    }

    private static Task CreateCurrentIndexes(MigrationContext context) =>
        CreateIndexes(
            context,
            true,
            CurrentNameIndexName,
            CurrentReferenceNumberIndexName,
            CurrentRecyclingObligationsIndexName,
            CurrentPercentageMetIndexName
        );

    private static Task CreateOptionalScopeIndexes(MigrationContext context) =>
        CreateIndexes(
            context,
            false,
            NameIndexName,
            ReferenceNumberIndexName,
            RecyclingObligationsIndexName,
            PercentageMetIndexName
        );

    private static async Task CreateIndexes(
        MigrationContext context,
        bool scoped,
        string nameIndexName,
        string referenceNumberIndexName,
        string recyclingObligationsIndexName,
        string percentageMetIndexName
    )
    {
        var indexKeys = Builders<OrganisationComplianceDeclarationEligibility>.IndexKeys;
        var prefix = CreatePrefix(indexKeys, scoped);
        await CreateIndex(
            context,
            nameIndexName,
            indexKeys.Combine(prefix, indexKeys.Ascending(x => x.Name), indexKeys.Ascending(x => x.OrganisationId))
        );
        await CreateIndex(
            context,
            referenceNumberIndexName,
            indexKeys.Combine(
                prefix,
                indexKeys.Ascending(x => x.ReferenceNumber),
                indexKeys.Ascending(x => x.Name),
                indexKeys.Ascending(x => x.OrganisationId)
            )
        );
        await CreateIndex(
            context,
            recyclingObligationsIndexName,
            indexKeys.Combine(
                prefix,
                indexKeys.Ascending(x => x.RecyclingObligationsMet),
                indexKeys.Ascending(x => x.Name),
                indexKeys.Ascending(x => x.OrganisationId)
            )
        );
        await CreateIndex(
            context,
            percentageMetIndexName,
            indexKeys.Combine(
                prefix,
                indexKeys.Ascending(x => x.ObligationCoveragePercentage),
                indexKeys.Ascending(x => x.Name),
                indexKeys.Ascending(x => x.OrganisationId)
            )
        );
    }

    private static IndexKeysDefinition<OrganisationComplianceDeclarationEligibility> CreatePrefix(
        IndexKeysDefinitionBuilder<OrganisationComplianceDeclarationEligibility> indexKeys,
        bool scoped
    ) =>
        scoped
            ? indexKeys
                .Ascending(x => x.Generation)
                .Ascending(x => x.ObligationYear)
                .Ascending(x => x.RegistrationType)
                .Ascending(x => x.IsVisibleInUnsubmittedView)
            : indexKeys.Ascending(x => x.Generation).Ascending(x => x.IsVisibleInUnsubmittedView);

    private static async Task DropIndexes(MigrationContext context, params string[] indexNames)
    {
        foreach (var indexName in indexNames)
        {
            await DropIndex<OrganisationComplianceDeclarationEligibility>(context, indexName);
        }
    }
}
