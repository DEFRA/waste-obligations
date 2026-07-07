using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(nameof(ComplianceDeclaration), MigrationDirection.Both)]
public class ComplianceDeclarationOrganisationReadIndex : MongoMigration
{
    private const string OrganisationIdObligationYearIndexName = "OrganisationId_ObligationYear";

    public override MigrationVersion Version => new(1, 0, 2);

    public override string Name => "003 - ComplianceDeclaration organisation read index";

    public override async Task UpAsync(MigrationContext context)
    {
        await DropIndex<ComplianceDeclaration>(context, OrganisationIdObligationYearIndexName);

        await CreateIndex(
            context,
            OrganisationIdObligationYearIndexName,
            Builders<ComplianceDeclaration>
                .IndexKeys.Ascending(x => x.Organisation.Id)
                .Ascending(x => x.ObligationYear)
                .Descending(x => x.Updated)
                .Ascending(x => x.Id)
        );
    }

    public override async Task DownAsync(MigrationContext context)
    {
        await DropIndex<ComplianceDeclaration>(context, OrganisationIdObligationYearIndexName);

        await CreateIndex(
            context,
            OrganisationIdObligationYearIndexName,
            Builders<ComplianceDeclaration>.IndexKeys.Ascending(x => x.Organisation.Id).Ascending(x => x.ObligationYear)
        );
    }
}
