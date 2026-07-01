using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.AuditEvents.Entities;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(nameof(AuditEvent), MigrationDirection.Both)]
public class AuditEventIndexes : MongoMigration
{
    public override MigrationVersion Version => new(1, 0, 1);

    public override string Name => "002 - AuditEvent indexes";

    public override async Task UpAsync(MigrationContext context)
    {
        foreach (var index in AuditEvents.AuditEventIndexes.All())
        {
            await CreateIndex(context, index.Name, index.Keys, index.Unique);
        }
    }

    public override async Task DownAsync(MigrationContext context)
    {
        foreach (var index in AuditEvents.AuditEventIndexes.All())
        {
            await DropIndex<AuditEvent>(context, index.Name);
        }
    }
}
