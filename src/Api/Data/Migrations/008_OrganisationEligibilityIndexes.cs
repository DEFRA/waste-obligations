using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(nameof(OrganisationComplianceDeclarationEligibility), MigrationDirection.Both)]
public class OrganisationEligibilityIndexes : MongoMigration
{
    private const string GenerationRowIndexName = "Generation_OrganisationId_ObligationYear_RegistrationType";
    private const string NameIndexName = "Generation_IsVisibleInUnsubmittedView_Name_OrganisationId";
    private const string PercentageMetIndexName =
        "Generation_IsVisibleInUnsubmittedView_ObligationCoveragePercentage_Name_OrganisationId";
    private const string RecyclingObligationsIndexName =
        "Generation_IsVisibleInUnsubmittedView_RecyclingObligationsMet_Name_OrganisationId";
    private const string ReferenceNumberIndexName =
        "Generation_IsVisibleInUnsubmittedView_ReferenceNumber_Name_OrganisationId";

    public override MigrationVersion Version => new(1, 0, 7);

    public override string Name => "008 - Organisation eligibility indexes";

    public override async Task UpAsync(MigrationContext context)
    {
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
        await CreateIndex(
            context,
            NameIndexName,
            Builders<OrganisationComplianceDeclarationEligibility>
                .IndexKeys.Ascending(x => x.Generation)
                .Ascending(x => x.IsVisibleInUnsubmittedView)
                .Ascending(x => x.Name)
                .Ascending(x => x.OrganisationId)
        );
        await CreateIndex(
            context,
            ReferenceNumberIndexName,
            Builders<OrganisationComplianceDeclarationEligibility>
                .IndexKeys.Ascending(x => x.Generation)
                .Ascending(x => x.IsVisibleInUnsubmittedView)
                .Ascending(x => x.ReferenceNumber)
                .Ascending(x => x.Name)
                .Ascending(x => x.OrganisationId)
        );
        await CreateIndex(
            context,
            RecyclingObligationsIndexName,
            Builders<OrganisationComplianceDeclarationEligibility>
                .IndexKeys.Ascending(x => x.Generation)
                .Ascending(x => x.IsVisibleInUnsubmittedView)
                .Ascending(x => x.RecyclingObligationsMet)
                .Ascending(x => x.Name)
                .Ascending(x => x.OrganisationId)
        );
        await CreateIndex(
            context,
            PercentageMetIndexName,
            Builders<OrganisationComplianceDeclarationEligibility>
                .IndexKeys.Ascending(x => x.Generation)
                .Ascending(x => x.IsVisibleInUnsubmittedView)
                .Ascending(x => x.ObligationCoveragePercentage)
                .Ascending(x => x.Name)
                .Ascending(x => x.OrganisationId)
        );
    }

    public override async Task DownAsync(MigrationContext context)
    {
        await DropIndex<OrganisationComplianceDeclarationEligibility>(context, GenerationRowIndexName);
        await DropIndex<OrganisationComplianceDeclarationEligibility>(context, NameIndexName);
        await DropIndex<OrganisationComplianceDeclarationEligibility>(context, ReferenceNumberIndexName);
        await DropIndex<OrganisationComplianceDeclarationEligibility>(context, RecyclingObligationsIndexName);
        await DropIndex<OrganisationComplianceDeclarationEligibility>(context, PercentageMetIndexName);
    }
}
