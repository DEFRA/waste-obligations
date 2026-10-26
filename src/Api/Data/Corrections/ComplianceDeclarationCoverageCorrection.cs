using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Utils.Logging;
using Defra.WasteObligations.AuditEvents;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Data.Corrections;

public class ComplianceDeclarationCoverageCorrection(
    IMongoDatabase database,
    IDbContext dbContext,
    IAuditEventService auditEventService,
    TimeProvider timeProvider
) : IApplicationDataCorrection
{
    private const string SchemaVersionField = "schemaVersion";
    private const string SchemaVersion = "v1.3";
    private const string ObligationsField = "obligations";
    private const string CoverageField = "obligationCoveragePercentage";
    private const string VersionField = "version";
    private const string UpdatedField = "updated";
    private const string IndexName = "SchemaVersion";

    public string Id => "compliance-declaration-individual-material-coverage";

    public async Task Apply(CancellationToken cancellationToken)
    {
        var collection = database.GetCollection<BsonDocument>(nameof(ComplianceDeclaration));
        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending(SchemaVersionField),
                new CreateIndexOptions { Name = IndexName }
            ),
            cancellationToken: cancellationToken
        );
        var filter = Builders<BsonDocument>.Filter.Eq(SchemaVersionField, SchemaVersion);
        using var cursor = await collection
            .Find(filter, new FindOptions { Hint = IndexName })
            .Project(Builders<BsonDocument>.Projection.Include("_id"))
            .ToCursorAsync(cancellationToken);
        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (var document in cursor.Current)
            {
                try
                {
                    await Recalculate(collection, document["_id"], cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    throw new InvalidOperationException(
                        $"Application data correction '{Id}' failed for compliance declaration '{document["_id"]}'.",
                        exception
                    );
                }
            }
        }

        await collection.Indexes.DropOneAsync(IndexName, cancellationToken);
    }

    private Task<bool> Recalculate(
        IMongoCollection<BsonDocument> collection,
        BsonValue id,
        CancellationToken cancellationToken
    ) =>
        dbContext.ExecuteTransaction(
            async (session, transactionCancellationToken) =>
            {
                var filter =
                    Builders<BsonDocument>.Filter.Eq("_id", id)
                    & Builders<BsonDocument>.Filter.Eq(SchemaVersionField, SchemaVersion);
                var before = await collection.Find(session, filter).SingleOrDefaultAsync(transactionCancellationToken);
                if (before is null)
                {
                    return false;
                }

                var obligations = before[ObligationsField].AsBsonArray;
                var percentage = Calculate(obligations);
                if (
                    before.TryGetValue(CoverageField, out var coverage)
                    && !coverage.IsBsonNull
                    && coverage.ToDecimal() == percentage
                )
                {
                    return false;
                }

                var version = before.TryGetValue(VersionField, out var storedVersion) ? storedVersion.ToInt32() + 1 : 2;
                var updated = timeProvider.GetUtcNowWithoutMicroseconds();
                var unchangedSnapshot =
                    filter
                    & Builders<BsonDocument>.Filter.Eq(ObligationsField, obligations)
                    & (
                        storedVersion is not null
                            ? Builders<BsonDocument>.Filter.Eq(VersionField, storedVersion)
                            : Builders<BsonDocument>.Filter.Exists(VersionField, false)
                    );
                var update = Builders<BsonDocument>
                    .Update.Set(CoverageField, new BsonDecimal128(percentage))
                    .Set(VersionField, version)
                    .Set(UpdatedField, updated);
                var result = await collection.UpdateOneAsync(
                    session,
                    unchangedSnapshot,
                    update,
                    cancellationToken: transactionCancellationToken
                );
                if (result.MatchedCount != 1)
                    throw new ConcurrencyException(
                        $"Compliance declaration '{id}' changed during coverage correction."
                    );

                var after = before.DeepClone().AsBsonDocument;
                after[CoverageField] = new BsonDecimal128(percentage);
                after[VersionField] = version;
                after[UpdatedField] = updated;
                await auditEventService.RecordEvent(
                    session,
                    new AuditEventRequest(
                        AuditActors.Service,
                        "compliance_declaration",
                        AuditEventOperation.Update,
                        "submission.amended",
                        null,
                        id.AsObjectId.ToString(),
                        version,
                        before,
                        after,
                        SchemaVersion,
                        updated
                    ),
                    transactionCancellationToken
                );

                return true;
            },
            $"compliance declaration coverage correction {id}",
            cancellationToken
        );

    private static decimal Calculate(BsonArray obligations) =>
        ObligationCoveragePercentageCalculator.CalculateFromMaterials(
            obligations.Select(x =>
            {
                var document = x.AsBsonDocument;
                var tonnages = document["tonnages"].AsBsonDocument;

                return (tonnages["accepted"].ToInt32(), tonnages["obligated"].ToInt32(), document["status"].AsString);
            })
        );
}
