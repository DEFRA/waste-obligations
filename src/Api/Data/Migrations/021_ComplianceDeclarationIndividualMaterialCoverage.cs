using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(nameof(ComplianceDeclaration), MigrationDirection.Both)]
public class ComplianceDeclarationIndividualMaterialCoverage : MongoMigration
{
    private const string SchemaVersionField = "schemaVersion";
    private const string SourceSchemaVersion = "v1.3";
    private const string TargetSchemaVersion = "v2.0";
    private const string ObligationsField = "obligations";
    private const string IndexName = "SchemaVersion";

    public override MigrationVersion Version => new(1, 0, 20);

    public override string Name => "021 - ComplianceDeclaration individual material coverage";

    public override async Task UpAsync(MigrationContext context)
    {
        var collection = context.Database.GetCollection<BsonDocument>(nameof(ComplianceDeclaration));
        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending(SchemaVersionField),
                new CreateIndexOptions { Name = IndexName }
            ),
            cancellationToken: context.CancellationToken
        );
        var filter = Builders<BsonDocument>.Filter.Eq(SchemaVersionField, SourceSchemaVersion);
        using var cursor = await collection
            .Find(filter, new FindOptions { Hint = IndexName })
            .ToCursorAsync(context.CancellationToken);
        while (await cursor.MoveNextAsync(context.CancellationToken))
        {
            foreach (var document in cursor.Current)
            {
                var obligations = document[ObligationsField].AsBsonArray;
                var percentage = ObligationCoveragePercentageCalculator.CalculateIndividualMaterialsFromBsonObligations(
                    obligations
                );
                var unchangedSnapshot =
                    filter
                    & Builders<BsonDocument>.Filter.Eq("_id", document["_id"])
                    & Builders<BsonDocument>.Filter.Eq(ObligationsField, obligations)
                    & (
                        document.TryGetValue("version", out var version)
                            ? Builders<BsonDocument>.Filter.Eq("version", version)
                            : Builders<BsonDocument>.Filter.Exists("version", false)
                    );
                var update = Builders<BsonDocument>
                    .Update.Set("obligationCoveragePercentage", new BsonDecimal128(percentage))
                    .Set(SchemaVersionField, TargetSchemaVersion);
                var result = await collection.UpdateOneAsync(
                    unchangedSnapshot,
                    update,
                    cancellationToken: context.CancellationToken
                );
                if (result.MatchedCount != 1)
                    throw new InvalidOperationException(
                        "A declaration changed during coverage migration; retry the migration from its remaining source-version documents."
                    );
            }
        }

        await collection.Indexes.DropOneAsync(IndexName, context.CancellationToken);
    }

    public override Task DownAsync(MigrationContext context) =>
        throw new NotSupportedException(
            "Individual-material coverage cannot be reversed: the original stored percentage was not retained. Reverting application code does not reverse the data migration."
        );
}
