using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(ObligationCalculationInstruction.CollectionName, MigrationDirection.Both)]
public class ObligationCalculationInstructionIndexes : MongoMigration
{
    private const string IndexName = "Active_ObligationYear_SubmitterId";

    public override MigrationVersion Version => new(1, 0, 20);

    public override string Name => "021 - Obligation calculation instruction indexes";

    public override async Task UpAsync(MigrationContext context)
    {
        var collection = context.Database.GetCollection<BsonDocument>(ObligationCalculationInstruction.CollectionName);
        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(
                new BsonDocument { { "obligationYear", 1 }, { "submitterId", 1 } },
                new CreateIndexOptions<BsonDocument>
                {
                    Name = IndexName,
                    Unique = true,
                    PartialFilterExpression = new BsonDocument(
                        "status",
                        new BsonDocument(
                            "$in",
                            new BsonArray
                            {
                                nameof(ObligationCalculationInstructionStatus.Pending),
                                nameof(ObligationCalculationInstructionStatus.Running),
                            }
                        )
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
