using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(nameof(OrganisationReferenceCache), MigrationDirection.Both)]
public class OrganisationReferenceCacheIndexes : MongoMigration
{
    private const string OrganisationRegistrationTypeIndexName = "OrganisationId_RegistrationType";
    private const string DueWorkIndexName = "ResolutionState_NextAttemptAt";

    public override MigrationVersion Version => new(1, 0, 8);

    public override string Name => "009 - Organisation reference cache indexes";

    public override async Task UpAsync(MigrationContext context)
    {
        await CreateIndex(
            context,
            OrganisationRegistrationTypeIndexName,
            Builders<OrganisationReferenceCache>
                .IndexKeys.Ascending(x => x.OrganisationId)
                .Ascending(x => x.RegistrationType),
            unique: true
        );
        await CreateIndex(
            context,
            DueWorkIndexName,
            Builders<OrganisationReferenceCache>
                .IndexKeys.Ascending(x => x.ResolutionState)
                .Ascending(x => x.NextAttemptAt)
        );
    }

    public override async Task DownAsync(MigrationContext context)
    {
        await DropIndex<OrganisationReferenceCache>(context, OrganisationRegistrationTypeIndexName);
        await DropIndex<OrganisationReferenceCache>(context, DueWorkIndexName);
    }
}
