using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

/// <summary>
/// The organisationName query parameter has been replaced by search, which matches the
/// organisation name, compliance scheme name, scheme operator name and reference number.
/// Organisation.Name is still searched, so the existing OrganisationName index from 001 is
/// kept; this adds the equivalent index for each of the other three fields so the planner
/// can scan index keys for every branch of the $or rather than the whole collection.
/// Indexes only, so an outgoing host running the previous version is unaffected.
/// </summary>
[MigrationCollection(nameof(ComplianceDeclaration), MigrationDirection.Both)]
public class ComplianceDeclarationSearchIndexes : MongoMigration
{
    private const string OrganisationComplianceSchemeNameIndexName = "OrganisationComplianceSchemeName";
    private const string OrganisationSchemeOperatorNameIndexName = "OrganisationSchemeOperatorName";
    private const string OrganisationReferenceNumberIndexName = "OrganisationReferenceNumber";

    public override MigrationVersion Version => new(1, 0, 6);

    public override string Name => "007 - ComplianceDeclaration search indexes";

    public override async Task UpAsync(MigrationContext context)
    {
        await CreateIndex(
            context,
            OrganisationComplianceSchemeNameIndexName,
            Builders<ComplianceDeclaration>.IndexKeys.Ascending(x => x.Organisation.ComplianceSchemeName)
        );

        await CreateIndex(
            context,
            OrganisationSchemeOperatorNameIndexName,
            Builders<ComplianceDeclaration>.IndexKeys.Ascending(x => x.Organisation.SchemeOperatorName)
        );

        await CreateIndex(
            context,
            OrganisationReferenceNumberIndexName,
            Builders<ComplianceDeclaration>.IndexKeys.Ascending(x => x.Organisation.ReferenceNumber)
        );
    }

    public override async Task DownAsync(MigrationContext context)
    {
        await DropIndex<ComplianceDeclaration>(context, OrganisationComplianceSchemeNameIndexName);
        await DropIndex<ComplianceDeclaration>(context, OrganisationSchemeOperatorNameIndexName);
        await DropIndex<ComplianceDeclaration>(context, OrganisationReferenceNumberIndexName);
    }
}
