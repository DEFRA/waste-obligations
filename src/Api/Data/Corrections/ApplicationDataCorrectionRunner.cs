using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Data.Corrections;

// Callers of Run and Revert must hold the Mongo migration lease. Startup runs corrections after schema migrations.
public class ApplicationDataCorrectionRunner(
    IMongoDatabase database,
    IEnumerable<IApplicationDataCorrection> corrections,
    TimeProvider timeProvider,
    ILogger<ApplicationDataCorrectionRunner> logger
)
{
    private const string CollectionName = "_application_data_corrections";

    public async Task Run(CancellationToken cancellationToken)
    {
        var collection = database.GetCollection<BsonDocument>(CollectionName);
        foreach (var correction in corrections)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", correction.Id);
            if (await collection.Find(filter).AnyAsync(cancellationToken))
                continue;

            await correction.Apply(cancellationToken);
            await collection.InsertOneAsync(
                new BsonDocument
                {
                    ["_id"] = correction.Id,
                    ["completedAt"] = timeProvider.GetUtcNowWithoutMicroseconds(),
                },
                cancellationToken: cancellationToken
            );
            logger.LogInformation("Application data correction {CorrectionId} completed.", correction.Id);
        }
    }

    // Explicit rollback records compensating events before allowing the correction to be applied again.
    public async Task Revert(string id, CancellationToken cancellationToken)
    {
        var correction = corrections.Single(x => x.Id == id);
        await correction.Revert(cancellationToken);
        var collection = database.GetCollection<BsonDocument>(CollectionName);
        await collection.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), cancellationToken);
        logger.LogInformation("Application data correction {CorrectionId} reverted.", id);
    }
}
