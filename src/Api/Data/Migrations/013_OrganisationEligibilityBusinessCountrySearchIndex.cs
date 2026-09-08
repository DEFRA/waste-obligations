using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(nameof(OrganisationComplianceDeclarationEligibility), MigrationDirection.Both)]
public class OrganisationEligibilityBusinessCountrySearchIndex : MongoMigration
{
    private const string IndexName = "Generation_IsVisibleInUnsubmittedView_BusinessCountry_Name_OrganisationId";

    public override MigrationVersion Version => new(1, 0, 12);

    public override string Name => "013 - Organisation eligibility business country search index";

    public override async Task UpAsync(MigrationContext context)
    {
        await CreateIndex(
            context,
            IndexName,
            Builders<OrganisationComplianceDeclarationEligibility>
                .IndexKeys.Ascending(x => x.Generation)
                .Ascending(x => x.IsVisibleInUnsubmittedView)
                .Ascending(x => x.BusinessCountry)
                .Ascending(x => x.Name)
                .Ascending(x => x.OrganisationId)
        );
    }

    public override async Task DownAsync(MigrationContext context) =>
        await DropIndex<OrganisationComplianceDeclarationEligibility>(context, IndexName);
}
