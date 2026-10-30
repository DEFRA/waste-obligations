using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(ObligationCalculationInstruction.CollectionName, MigrationDirection.Both)]
public class ObligationCalculationInstructionRetentionIndex : MongoMigration
{
    private const string IndexName = "CreatedAt_Terminal_Retention";

    public override MigrationVersion Version => new(1, 0, 21);

    public override string Name => "022 - Obligation calculation instruction retention index";

    public override async Task UpAsync(MigrationContext context)
    {
        var collection = context.Database.GetCollection<BsonDocument>(ObligationCalculationInstruction.CollectionName);
        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(
                new BsonDocument("createdAt", 1),
                new CreateIndexOptions<BsonDocument>
                {
                    Name = IndexName,
                    ExpireAfter = TimeSpan.FromDays(365),
                    PartialFilterExpression = new BsonDocument(
                        "status",
                        new BsonDocument("$in", new BsonArray { "Completed", "Failed" })
                    ),
                }
            ),
            cancellationToken: context.CancellationToken
        );
    }

    public override Task DownAsync(MigrationContext context) =>
        DropIndex<ObligationCalculationInstruction>(
            context,
            ObligationCalculationInstruction.CollectionName,
            IndexName
        );
}
