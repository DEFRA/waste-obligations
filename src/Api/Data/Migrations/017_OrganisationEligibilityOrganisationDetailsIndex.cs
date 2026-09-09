using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(nameof(OrganisationComplianceDeclarationEligibility), MigrationDirection.Both)]
public class OrganisationEligibilityOrganisationDetailsIndex : MongoMigration
{
    private const string IndexName = "OrganisationId_RefreshedAt_ObligationYear_RegistrationType_Generation";

    public override MigrationVersion Version => new(1, 0, 16);

    public override string Name => "017 - Organisation eligibility organisation details index";

    public override async Task UpAsync(MigrationContext context)
    {
        await CreateIndex(
            context,
            IndexName,
            Builders<OrganisationComplianceDeclarationEligibility>
                .IndexKeys.Ascending(x => x.OrganisationId)
                .Descending(x => x.RefreshedAt)
                .Descending(x => x.ObligationYear)
                .Ascending(x => x.RegistrationType)
                .Ascending(x => x.Generation)
        );
    }

    public override async Task DownAsync(MigrationContext context) =>
        await DropIndex<OrganisationComplianceDeclarationEligibility>(context, IndexName);
}
