using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(nameof(OrganisationObligationSummary), MigrationDirection.Both)]
public class OrganisationObligationSummaryIndexes : MongoMigration
{
    private const string OrganisationYearIndexName = "OrganisationId_ObligationYear";

    public override MigrationVersion Version => new(1, 0, 9);

    public override string Name => "010 - Organisation obligation summary indexes";

    public override async Task UpAsync(MigrationContext context)
    {
        await CreateIndex(
            context,
            OrganisationYearIndexName,
            Builders<OrganisationObligationSummary>
                .IndexKeys.Ascending(x => x.OrganisationId)
                .Ascending(x => x.ObligationYear),
            unique: true
        );
    }

    public override async Task DownAsync(MigrationContext context) =>
        await DropIndex<OrganisationObligationSummary>(context, OrganisationYearIndexName);
}
