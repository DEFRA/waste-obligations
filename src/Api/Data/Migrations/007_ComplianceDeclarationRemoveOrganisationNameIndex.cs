using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

/// <summary>
/// The OrganisationName index existed to serve the organisationName filter, which the
/// search parameter has replaced. Search matches four organisation fields with a
/// case-insensitive contains, which cannot seek an index, so the index can no longer be
/// used and only costs write throughput. Filtering happens on obligation year, status and
/// registration type first, which the ObligationYear_Status_OrganisationRegistrationType
/// index does serve, leaving a small set to scan.
/// Index removal only, so an outgoing host running the previous version is unaffected.
/// </summary>
[MigrationCollection(nameof(ComplianceDeclaration), MigrationDirection.Both)]
public class ComplianceDeclarationRemoveOrganisationNameIndex : MongoMigration
{
    private const string OrganisationNameIndexName = "OrganisationName";

    public override MigrationVersion Version => new(1, 0, 6);

    public override string Name => "007 - Remove ComplianceDeclaration organisation name index";

    public override async Task UpAsync(MigrationContext context) =>
        await DropIndex<ComplianceDeclaration>(context, OrganisationNameIndexName);

    public override async Task DownAsync(MigrationContext context) =>
        await CreateIndex(
            context,
            OrganisationNameIndexName,
            Builders<ComplianceDeclaration>.IndexKeys.Ascending(x => x.Organisation.Name)
        );
}
