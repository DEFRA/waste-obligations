using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(nameof(OrganisationObligationSummary), MigrationDirection.Both)]
public class OrganisationObligationSummaryHistoricalBackfillIndex : MongoMigration
{
    private const string IndexName = "ObligationYear_RequestedAt_IsHydrationActive_Priority_NextRefreshAt";

    public override MigrationVersion Version => new(1, 0, 17);

    public override string Name => "018 - Organisation obligation summary historical backfill index";

    public override async Task UpAsync(MigrationContext context)
    {
        await CreateIndex(
            context,
            IndexName,
            Builders<OrganisationObligationSummary>
                .IndexKeys.Ascending(x => x.ObligationYear)
                .Ascending(x => x.RequestedAt)
                .Ascending(x => x.IsHydrationActive)
                .Ascending(x => x.Priority)
                .Ascending(x => x.NextRefreshAt)
        );
    }

    public override async Task DownAsync(MigrationContext context) =>
        await DropIndex<OrganisationObligationSummary>(context, IndexName);
}
