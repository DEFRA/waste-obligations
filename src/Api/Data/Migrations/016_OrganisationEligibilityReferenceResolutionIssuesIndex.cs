using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(nameof(OrganisationComplianceDeclarationEligibility), MigrationDirection.Both)]
public class OrganisationEligibilityReferenceResolutionIssuesIndex : MongoMigration
{
    private const string IndexName =
        "Generation_ReferenceNumberResolutionState_OrganisationId_ObligationYear_RegistrationType";

    public override MigrationVersion Version => new(1, 0, 15);

    public override string Name => "016 - Organisation eligibility reference resolution issues index";

    public override async Task UpAsync(MigrationContext context)
    {
        await CreateIndex(
            context,
            IndexName,
            Builders<OrganisationComplianceDeclarationEligibility>
                .IndexKeys.Ascending(x => x.Generation)
                .Ascending(x => x.ReferenceNumberResolutionState)
                .Ascending(x => x.OrganisationId)
                .Ascending(x => x.ObligationYear)
                .Ascending(x => x.RegistrationType)
        );
    }

    public override async Task DownAsync(MigrationContext context) =>
        await DropIndex<OrganisationComplianceDeclarationEligibility>(context, IndexName);
}
