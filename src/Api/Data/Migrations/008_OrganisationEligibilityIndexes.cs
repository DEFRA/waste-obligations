using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(nameof(OrganisationComplianceDeclarationEligibility), MigrationDirection.Both)]
public class OrganisationEligibilityIndexes : MongoMigration
{
    private const string QueryIndexName =
        "Generation_ObligationYear_RegistrationType_RegistrationStatus_ReferenceNumberResolutionState_Name_OrganisationId";
    private const string GenerationRowIndexName = "Generation_OrganisationId_ObligationYear_RegistrationType";

    public override MigrationVersion Version => new(1, 0, 7);

    public override string Name => "008 - Organisation eligibility indexes";

    public override async Task UpAsync(MigrationContext context)
    {
        await CreateIndex(
            context,
            QueryIndexName,
            Builders<OrganisationComplianceDeclarationEligibility>
                .IndexKeys.Ascending(x => x.Generation)
                .Ascending(x => x.ObligationYear)
                .Ascending(x => x.RegistrationType)
                .Ascending(x => x.RegistrationStatus)
                .Ascending(x => x.ReferenceNumberResolutionState)
                .Ascending(x => x.Name)
                .Ascending(x => x.OrganisationId)
        );
        await CreateIndex(
            context,
            GenerationRowIndexName,
            Builders<OrganisationComplianceDeclarationEligibility>
                .IndexKeys.Ascending(x => x.Generation)
                .Ascending(x => x.OrganisationId)
                .Ascending(x => x.ObligationYear)
                .Ascending(x => x.RegistrationType),
            unique: true
        );
    }

    public override async Task DownAsync(MigrationContext context)
    {
        await DropIndex<OrganisationComplianceDeclarationEligibility>(context, QueryIndexName);
        await DropIndex<OrganisationComplianceDeclarationEligibility>(context, GenerationRowIndexName);
    }
}
