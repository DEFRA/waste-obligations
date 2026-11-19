using System.Diagnostics;
using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

/// <summary>
/// Instructions written before correlation IDs were recorded may still be pending or running. Persist a
/// generated trace ID for each so every attempt and every calculation event from that instruction shares it.
/// </summary>
[MigrationCollection(ObligationCalculationInstruction.CollectionName, MigrationDirection.Both)]
public class ObligationCalculationInstructionCorrelationIds : MongoMigration
{
    private const string CorrelationIdField = "correlationId";

    public override MigrationVersion Version => new(1, 0, 23);

    public override string Name => "024 - Obligation calculation instruction correlation IDs";

    public override async Task UpAsync(MigrationContext context)
    {
        var collection = context.Database.GetCollection<BsonDocument>(ObligationCalculationInstruction.CollectionName);
        var missing = new BsonArray { BsonNull.Value, string.Empty };
        var filter = new BsonDocument
        {
            {
                "status",
                new BsonDocument(
                    "$in",
                    new BsonArray
                    {
                        nameof(ObligationCalculationInstructionStatus.Pending),
                        nameof(ObligationCalculationInstructionStatus.Running),
                    }
                )
            },
            { CorrelationIdField, new BsonDocument("$in", missing) },
        };
        var ids = await collection
            .Find(filter)
            .Project(new BsonDocument("_id", 1))
            .ToListAsync(context.CancellationToken);

        foreach (var id in ids.Select(x => x["_id"]))
        {
            // Only fill a value that is still missing, so a concurrently assigned ID is preserved.
            await collection.UpdateOneAsync(
                new BsonDocument { { "_id", id }, { CorrelationIdField, new BsonDocument("$in", missing) } },
                new BsonDocument(
                    "$set",
                    new BsonDocument(CorrelationIdField, ActivityTraceId.CreateRandom().ToString())
                ),
                cancellationToken: context.CancellationToken
            );
        }
    }

    // Generated correlation IDs are indistinguishable from recorded ones, so the backfill is not reversed.
    public override Task DownAsync(MigrationContext context) => Task.CompletedTask;
}
