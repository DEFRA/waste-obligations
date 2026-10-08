using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;
using ComplianceDeclaration = Defra.WasteObligations.Api.Data.Entities.ComplianceDeclaration;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(nameof(ComplianceDeclaration), MigrationDirection.Both)]
public class ComplianceDeclarationObligationCoveragePercentage : MongoMigration
{
    private const string SchemaVersionField = "schemaVersion";
    private const string ObligationCoveragePercentageField = "obligationCoveragePercentage";
    private const string ObligationsField = "obligations";
    private const string SchemaVersionV1_1 = "v1.1";
    private const string SchemaVersionV1_2 = "v1.2";

    public override MigrationVersion Version => new(1, 0, 3);

    public override string Name => "004 - ComplianceDeclaration obligation coverage percentage";

    public override async Task UpAsync(MigrationContext context)
    {
        var collection = context.Database.GetCollection<BsonDocument>(nameof(ComplianceDeclaration));
        var filter = Builders<BsonDocument>.Filter.Eq(SchemaVersionField, SchemaVersionV1_1);

        using var cursor = await collection.Find(filter).ToCursorAsync(context.CancellationToken);
        while (await cursor.MoveNextAsync(context.CancellationToken))
        {
            foreach (var document in cursor.Current)
            {
                var obligationCoveragePercentage = document.Contains(ObligationCoveragePercentageField)
                    ? document[ObligationCoveragePercentageField].ToDecimal()
                    : ObligationCoveragePercentageCalculator.CalculateFromBsonObligations(
                        document[ObligationsField].AsBsonArray
                    );
                var update = Builders<BsonDocument>
                    .Update.Set(SchemaVersionField, SchemaVersionV1_2)
                    .Set(ObligationCoveragePercentageField, new BsonDecimal128(obligationCoveragePercentage));

                await collection.UpdateOneAsync(
                    Builders<BsonDocument>.Filter.Eq("_id", document["_id"]),
                    update,
                    cancellationToken: context.CancellationToken
                );
            }
        }
    }

    public override async Task DownAsync(MigrationContext context)
    {
        var collection = context.Database.GetCollection<BsonDocument>(nameof(ComplianceDeclaration));
        var filter = Builders<BsonDocument>.Filter.Eq(SchemaVersionField, SchemaVersionV1_2);
        var update = Builders<BsonDocument>
            .Update.Set(SchemaVersionField, SchemaVersionV1_1)
            .Unset(ObligationCoveragePercentageField);

        await collection.UpdateManyAsync(filter, update, cancellationToken: context.CancellationToken);
    }
}
