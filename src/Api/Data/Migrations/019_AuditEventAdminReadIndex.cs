using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.AuditEvents.Entities;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(nameof(AuditEvent), MigrationDirection.Both)]
public class AuditEventAdminReadIndex : MongoMigration
{
    private const string IndexName = "Entity_EntityId_Sequence";

    public override MigrationVersion Version => new(1, 0, 18);

    public override string Name => "019 - Audit event admin read index";

    public override async Task UpAsync(MigrationContext context)
    {
        await CreateIndex(
            context,
            IndexName,
            Builders<AuditEvent>
                .IndexKeys.Ascending(x => x.Entity)
                .Ascending(x => x.EntityId)
                .Ascending(x => x.Sequence)
        );
    }

    public override async Task DownAsync(MigrationContext context) => await DropIndex<AuditEvent>(context, IndexName);
}
