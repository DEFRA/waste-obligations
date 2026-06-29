using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(nameof(ComplianceDeclaration), MigrationDirection.Both)]
public class ComplianceDeclarationIndexes : MongoMigration
{
    private const string OrganisationIdObligationYearIndexName = "OrganisationId_ObligationYear";
    private const string SearchIndexName = "ObligationYear_Status_OrganisationRegistrationType";
    private const string OrganisationNameIndexName = "OrganisationName";

    public override MigrationVersion Version => new(1, 0, 0);

    public override string Name => "001 - ComplianceDeclaration indexes";

    public override async Task UpAsync(MigrationContext context)
    {
        await CreateIndex(
            context,
            OrganisationIdObligationYearIndexName,
            Builders<ComplianceDeclaration>.IndexKeys.Ascending(x => x.Organisation.Id).Ascending(x => x.ObligationYear)
        );

        await CreateIndex(
            context,
            SearchIndexName,
            Builders<ComplianceDeclaration>
                .IndexKeys.Ascending(x => x.ObligationYear)
                .Ascending(x => x.Status)
                .Ascending(x => x.Organisation.RegistrationType)
        );

        await CreateIndex(
            context,
            OrganisationNameIndexName,
            Builders<ComplianceDeclaration>.IndexKeys.Ascending(x => x.Organisation.Name)
        );
    }

    public override async Task DownAsync(MigrationContext context)
    {
        await DropIndex<ComplianceDeclaration>(context, OrganisationIdObligationYearIndexName);
        await DropIndex<ComplianceDeclaration>(context, SearchIndexName);
        await DropIndex<ComplianceDeclaration>(context, OrganisationNameIndexName);
    }
}
