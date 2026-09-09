using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(nameof(OrganisationObligationSummary), MigrationDirection.Both)]
public class OrganisationObligationSummaryFailedAdminReadIndex : MongoMigration
{
    private const string IndexName = "RefreshState_ObligationYear_NextRefreshAt_OrganisationId";

    public override MigrationVersion Version => new(1, 0, 19);

    public override string Name => "020 - Organisation obligation summary failed admin read index";

    public override async Task UpAsync(MigrationContext context)
    {
        await CreateIndex(
            context,
            IndexName,
            Builders<OrganisationObligationSummary>
                .IndexKeys.Ascending(x => x.RefreshState)
                .Ascending(x => x.ObligationYear)
                .Ascending(x => x.NextRefreshAt)
                .Ascending(x => x.OrganisationId)
        );
    }

    public override async Task DownAsync(MigrationContext context) =>
        await DropIndex<OrganisationObligationSummary>(context, IndexName);
}
