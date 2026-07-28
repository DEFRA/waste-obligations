using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;
using ComplianceDeclaration = Defra.WasteObligations.Api.Data.Entities.ComplianceDeclaration;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(nameof(ComplianceDeclaration), MigrationDirection.Both)]
public class ComplianceDeclarationObligationCoveragePercentagePrecision : MongoMigration
{
    private const int PreviousDecimalPlaces = 2;
    private const string SchemaVersionField = "schemaVersion";
    private const string ObligationCoveragePercentageField = "obligationCoveragePercentage";
    private const string ObligationsField = "obligations";
    private const string SchemaVersionV1_2 = "v1.2";

    public override MigrationVersion Version => new(1, 0, 4);

    public override string Name => "005 - ComplianceDeclaration obligation coverage percentage precision";

    public override async Task UpAsync(MigrationContext context)
    {
        var collection = context.Database.GetCollection<BsonDocument>(nameof(ComplianceDeclaration));
        var filter = Builders<BsonDocument>.Filter.Eq(SchemaVersionField, SchemaVersionV1_2);

        using var cursor = await collection.Find(filter).ToCursorAsync(context.CancellationToken);
        while (await cursor.MoveNextAsync(context.CancellationToken))
        {
            foreach (var document in cursor.Current)
            {
                var obligationCoveragePercentage = ObligationCoveragePercentageCalculator.CalculateFromBsonObligations(
                    document[ObligationsField].AsBsonArray
                );
                var update = Builders<BsonDocument>.Update.Set(
                    ObligationCoveragePercentageField,
                    new BsonDecimal128(obligationCoveragePercentage)
                );

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

        using var cursor = await collection.Find(filter).ToCursorAsync(context.CancellationToken);
        while (await cursor.MoveNextAsync(context.CancellationToken))
        {
            foreach (var document in cursor.Current)
            {
                var obligationCoveragePercentage = ObligationCoveragePercentageCalculator.CalculateFromBsonObligations(
                    document[ObligationsField].AsBsonArray,
                    PreviousDecimalPlaces
                );
                var update = Builders<BsonDocument>.Update.Set(
                    ObligationCoveragePercentageField,
                    new BsonDecimal128(obligationCoveragePercentage)
                );

                await collection.UpdateOneAsync(
                    Builders<BsonDocument>.Filter.Eq("_id", document["_id"]),
                    update,
                    cancellationToken: context.CancellationToken
                );
            }
        }
    }
}
