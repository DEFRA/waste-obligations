using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(nameof(ComplianceDeclaration), MigrationDirection.Both)]
public class ComplianceDeclarationBusinessCountrySearchIndex : MongoMigration
{
    private const string IndexName = "BusinessCountry_ObligationYear_Status_OrganisationRegistrationType";

    public override MigrationVersion Version => new(1, 0, 8);

    public override string Name => "009 - ComplianceDeclaration business country search index";

    public override async Task UpAsync(MigrationContext context)
    {
        await CreateIndex(
            context,
            IndexName,
            Builders<ComplianceDeclaration>
                .IndexKeys.Ascending(x => x.Organisation.BusinessCountry)
                .Ascending(x => x.ObligationYear)
                .Ascending(x => x.Status)
                .Ascending(x => x.Organisation.RegistrationType)
        );
    }

    public override async Task DownAsync(MigrationContext context) =>
        await DropIndex<ComplianceDeclaration>(context, IndexName);
}
