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
    private const string ObligationsField = "obligations";
    private const string IndexName = "SchemaVersion";
    private const string VersionField = "version";

    public override MigrationVersion Version => new(1, 0, 20);

    public override string Name => "021 - ComplianceDeclaration individual material coverage";

    public override Task UpAsync(MigrationContext context) => Recalculate(context, CalculateIndividualMaterials);

    // Reapply the historical formula; arbitrary values previously stored by a caller are not retained.
    public override Task DownAsync(MigrationContext context) =>
        Recalculate(context, HistoricalObligationCoveragePercentageCalculator.CalculateFromBsonObligations);

    private static async Task Recalculate(MigrationContext context, Func<BsonArray, decimal> calculate)
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
                var percentage = calculate(obligations);
                var unchangedSnapshot =
                    filter
                    & Builders<BsonDocument>.Filter.Eq("_id", document["_id"])
                    & Builders<BsonDocument>.Filter.Eq(ObligationsField, obligations)
                    & (
                        document.TryGetValue(VersionField, out var version)
                            ? Builders<BsonDocument>.Filter.Eq(VersionField, version)
                            : Builders<BsonDocument>.Filter.Exists(VersionField, false)
                    );
                var update = Builders<BsonDocument>.Update.Set(
                    "obligationCoveragePercentage",
                    new BsonDecimal128(percentage)
                );
                var result = await collection.UpdateOneAsync(
                    unchangedSnapshot,
                    update,
                    cancellationToken: context.CancellationToken
                );
                if (result.MatchedCount != 1)
                    throw new InvalidOperationException(
                        "A declaration changed during coverage migration; retry the migration to rescan the current snapshots."
                    );
            }
        }

        await collection.Indexes.DropOneAsync(IndexName, context.CancellationToken);
    }

    private static decimal CalculateIndividualMaterials(BsonArray obligations) =>
        ObligationCoveragePercentageCalculator.CalculateFromMaterials(
            obligations.Select(x =>
            {
                var document = x.AsBsonDocument;
                var tonnages = document["tonnages"].AsBsonDocument;

                return (tonnages["accepted"].ToInt32(), tonnages["obligated"].ToInt32(), document["status"].AsString);
            })
        );
}
