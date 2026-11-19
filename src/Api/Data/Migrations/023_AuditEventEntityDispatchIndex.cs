using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.AuditEvents.Entities;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

// Entity-filtered dispatch reads replace the two analytics dispatch indexes from 002.
[MigrationCollection(nameof(AuditEvent), MigrationDirection.Both)]
public class AuditEventEntityDispatchIndex : MongoMigration
{
    private static readonly string[] ProcessNames = ["analytics", "calculation"];
    private static readonly string[] LegacyDispatchIndexNames =
    [
        "Dispatch_analytics",
        "Dispatch_analytics_Status_NextAttemptAt_Sequence",
    ];
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

        foreach (var name in LegacyDispatchIndexNames)
            await DropIndex<AuditEvent>(context, name);
    }

    public override async Task DownAsync(MigrationContext context)
    {
        foreach (var index in AuditEvents.AuditEventIndexes.All().Where(x => LegacyDispatchIndexNames.Contains(x.Name)))
            await CreateIndex(context, index.Name, index.Keys, index.Unique);

        foreach (var processName in ProcessNames)
        {
            await DropIndex<AuditEvent>(context, $"Dispatch_{processName}_Entity_Sequence");
            await DropIndex<AuditEvent>(context, $"Dispatch_{processName}_Status_Entity_Sequence_NextAttemptAt");
        }
    }
}
