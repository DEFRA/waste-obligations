using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(nameof(OrganisationObligationSummary), MigrationDirection.Both)]
public class OrganisationObligationSummaryHydrationIndexes : MongoMigration
{
    private const string DueWorkIndexName = "IsHydrationActive_NextRefreshAt_Priority";

    public override MigrationVersion Version => new(1, 0, 11);

    public override string Name => "012 - Organisation obligation summary hydration indexes";

    public override async Task UpAsync(MigrationContext context)
    {
        await CreateIndex(
            context,
            DueWorkIndexName,
            Builders<OrganisationObligationSummary>
                .IndexKeys.Ascending(x => x.IsHydrationActive)
                .Ascending(x => x.NextRefreshAt)
                .Ascending(x => x.Priority)
        );
    }

    public override async Task DownAsync(MigrationContext context) =>
        await DropIndex<OrganisationObligationSummary>(context, DueWorkIndexName);
}
