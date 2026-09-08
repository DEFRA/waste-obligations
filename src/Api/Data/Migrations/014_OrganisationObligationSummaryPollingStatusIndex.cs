using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(nameof(OrganisationObligationSummary), MigrationDirection.Both)]
public class OrganisationObligationSummaryPollingStatusIndex : MongoMigration
{
    private const string IndexName = "IsHydrationActive_ObligationYear_NextRefreshAt";

    public override MigrationVersion Version => new(1, 0, 13);

    public override string Name => "014 - Organisation obligation summary polling status index";

    public override async Task UpAsync(MigrationContext context)
    {
        await CreateIndex(
            context,
            IndexName,
            Builders<OrganisationObligationSummary>
                .IndexKeys.Ascending(x => x.IsHydrationActive)
                .Ascending(x => x.ObligationYear)
                .Ascending(x => x.NextRefreshAt)
        );
    }

    public override async Task DownAsync(MigrationContext context) =>
        await DropIndex<OrganisationObligationSummary>(context, IndexName);
}
