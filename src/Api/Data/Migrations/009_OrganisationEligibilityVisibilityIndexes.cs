using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(nameof(OrganisationComplianceDeclarationEligibility), MigrationDirection.Both)]
public class OrganisationEligibilityVisibilityIndexes : MongoMigration
{
    private const string PreviousQueryIndexName =
        "Generation_ObligationYear_RegistrationType_RegistrationStatus_ReferenceNumberResolutionState_Name_OrganisationId";
    private const string QueryIndexName =
        "Generation_ObligationYear_RegistrationType_IsVisibleInUnsubmittedView_Name_OrganisationId";

    public override MigrationVersion Version => new(1, 0, 8);

    public override string Name => "009 - Organisation eligibility visibility indexes";

    public override async Task UpAsync(MigrationContext context)
    {
        await DropIndex<OrganisationComplianceDeclarationEligibility>(context, PreviousQueryIndexName);
        await CreateIndex(
            context,
            QueryIndexName,
            Builders<OrganisationComplianceDeclarationEligibility>
                .IndexKeys.Ascending(x => x.Generation)
                .Ascending(x => x.ObligationYear)
                .Ascending(x => x.RegistrationType)
                .Ascending(x => x.IsVisibleInUnsubmittedView)
                .Ascending(x => x.Name)
                .Ascending(x => x.OrganisationId)
        );
    }

    public override async Task DownAsync(MigrationContext context)
    {
        await DropIndex<OrganisationComplianceDeclarationEligibility>(context, QueryIndexName);
        await CreateIndex(
            context,
            PreviousQueryIndexName,
            Builders<OrganisationComplianceDeclarationEligibility>
                .IndexKeys.Ascending(x => x.Generation)
                .Ascending(x => x.ObligationYear)
                .Ascending(x => x.RegistrationType)
                .Ascending(x => x.RegistrationStatus)
                .Ascending(x => x.ReferenceNumberResolutionState)
                .Ascending(x => x.Name)
                .Ascending(x => x.OrganisationId)
        );
    }
}
