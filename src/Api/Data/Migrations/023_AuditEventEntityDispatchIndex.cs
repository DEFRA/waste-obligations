using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.AuditEvents.Entities;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

// Dispatch reads now filter by entity, so these indexes supersede the analytics dispatch indexes from 002.
// Those are kept while hosts that read without the entity filter may still run; drop them in a later migration.
[MigrationCollection(nameof(AuditEvent), MigrationDirection.Both)]
public class AuditEventEntityDispatchIndex : MongoMigration
{
    private static readonly string[] ProcessNames = ["analytics", "calculation"];
    public override MigrationVersion Version => new(1, 0, 22);
    public override string Name => "023 - Audit event entity dispatch indexes";

    public override async Task UpAsync(MigrationContext context)
    {
        foreach (var processName in ProcessNames)
        {
            await CreateIndex(
                context,
                $"Dispatch_{processName}_Entity_Sequence",
                Builders<AuditEvent>
                    .IndexKeys.Ascending($"dispatches.{processName}")
                    .Ascending(x => x.Entity)
                    .Ascending(x => x.Sequence)
            );
            await CreateIndex(
                context,
                $"Dispatch_{processName}_Status_Entity_Sequence_NextAttemptAt",
                Builders<AuditEvent>
                    .IndexKeys.Ascending($"dispatches.{processName}.status")
                    .Ascending(x => x.Entity)
                    .Ascending(x => x.Sequence)
                    .Ascending($"dispatches.{processName}.nextAttemptAt")
            );
        }
    }

    public override async Task DownAsync(MigrationContext context)
    {
        foreach (var processName in ProcessNames)
        {
            await DropIndex<AuditEvent>(context, $"Dispatch_{processName}_Entity_Sequence");
            await DropIndex<AuditEvent>(context, $"Dispatch_{processName}_Status_Entity_Sequence_NextAttemptAt");
        }
    }
}
