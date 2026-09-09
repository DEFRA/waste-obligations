using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(OrganisationObligationHistoricalBackfill.CollectionName, MigrationDirection.Both)]
public class OrganisationObligationHistoricalBackfillIndexes : MongoMigration
{
    private const string IncompleteWorkIndexName = "CompletedAt_RequestedAt";

    public override MigrationVersion Version => new(1, 0, 14);

    public override string Name => "015 - Organisation obligation historical backfill indexes";

    public override async Task UpAsync(MigrationContext context)
    {
        await CreateIndex(
            context,
            OrganisationObligationHistoricalBackfill.CollectionName,
            IncompleteWorkIndexName,
            Builders<OrganisationObligationHistoricalBackfill>
                .IndexKeys.Ascending(x => x.CompletedAt)
                .Ascending(x => x.RequestedAt)
        );
    }

    public override async Task DownAsync(MigrationContext context) =>
        await DropIndex<OrganisationObligationHistoricalBackfill>(
            context,
            OrganisationObligationHistoricalBackfill.CollectionName,
            IncompleteWorkIndexName
        );
}
