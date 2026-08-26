using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(nameof(ComplianceDeclarationReviewState), MigrationDirection.Both)]
public class ComplianceDeclarationReviewStateIndexes : MongoMigration
{
    private const string OrganisationYearRegistrationTypeIndexName = "OrganisationId_ObligationYear_RegistrationType";

    public override MigrationVersion Version => new(1, 0, 9);

    public override string Name => "010 - Compliance declaration review state indexes";

    public override async Task UpAsync(MigrationContext context)
    {
        await CreateIndex(
            context,
            OrganisationYearRegistrationTypeIndexName,
            Builders<ComplianceDeclarationReviewState>
                .IndexKeys.Ascending(x => x.OrganisationId)
                .Ascending(x => x.ObligationYear)
                .Ascending(x => x.RegistrationType),
            unique: true
        );
    }

    public override async Task DownAsync(MigrationContext context)
    {
        await DropIndex<ComplianceDeclarationReviewState>(context, OrganisationYearRegistrationTypeIndexName);
    }
}
