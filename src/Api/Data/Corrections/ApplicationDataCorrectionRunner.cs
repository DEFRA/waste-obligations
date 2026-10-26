using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Data.Corrections;

// Runs under the Mongo migration lease, after schema migrations have completed.
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
}
