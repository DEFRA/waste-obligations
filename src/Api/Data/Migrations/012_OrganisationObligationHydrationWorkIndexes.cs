using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(nameof(OrganisationObligationHydrationWork), MigrationDirection.Both)]
public class OrganisationObligationHydrationWorkIndexes : MongoMigration
{
    private const string OrganisationYearIndexName = "OrganisationId_ObligationYear";
    private const string DueWorkIndexName = "NextAttemptAt_Priority";

    public override MigrationVersion Version => new(1, 0, 11);

    public override string Name => "012 - Organisation obligation hydration work indexes";

    public override async Task UpAsync(MigrationContext context)
    {
        await CreateIndex(
            context,
            OrganisationYearIndexName,
            Builders<OrganisationObligationHydrationWork>
                .IndexKeys.Ascending(x => x.OrganisationId)
                .Ascending(x => x.ObligationYear),
            unique: true
        );
        await CreateIndex(
            context,
            DueWorkIndexName,
            Builders<OrganisationObligationHydrationWork>
                .IndexKeys.Ascending(x => x.NextAttemptAt)
                .Ascending(x => x.Priority)
        );
    }

    public override async Task DownAsync(MigrationContext context)
    {
        await DropIndex<OrganisationObligationHydrationWork>(context, OrganisationYearIndexName);
        await DropIndex<OrganisationObligationHydrationWork>(context, DueWorkIndexName);
    }
}
