using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(nameof(ObligationCalculation), MigrationDirection.Both)]
public class ObligationCalculationIndexes : MongoMigration
{
    public const string IdentityIndexName = "SubmitterOrganisationId_ObligationYear";
    private const string ProducerIndexName = "ProducerOrganisationId";
    private const string HistoryIndexName = "CalculationId_Version";
    public override MigrationVersion Version => new(1, 0, 21);
    public override string Name => "022 - Obligation calculation indexes";

    public override async Task UpAsync(MigrationContext context)
    {
        await context
            .Database.GetCollection<BsonDocument>(nameof(ObligationCalculation))
            .Indexes.CreateManyAsync(
                [
                    new CreateIndexModel<BsonDocument>(
                        new BsonDocument { { "submitterOrganisationId", 1 }, { "obligationYear", 1 } },
                        new CreateIndexOptions { Name = IdentityIndexName, Unique = true }
                    ),
                    new CreateIndexModel<BsonDocument>(
                        new BsonDocument("producers.producerOrganisationId", 1),
                        new CreateIndexOptions { Name = ProducerIndexName }
                    ),
                ],
                cancellationToken: context.CancellationToken
            );
        await context
            .Database.GetCollection<BsonDocument>(nameof(ObligationCalculationHistory))
            .Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    new BsonDocument { { "calculation._id", 1 }, { "calculation.version", 1 } },
                    new CreateIndexOptions { Name = HistoryIndexName, Unique = true }
                ),
                cancellationToken: context.CancellationToken
            );
    }

    public override async Task DownAsync(MigrationContext context)
    {
        await DropIndex<ObligationCalculationHistory>(context, nameof(ObligationCalculationHistory), HistoryIndexName);
        await DropIndex<ObligationCalculation>(context, nameof(ObligationCalculation), ProducerIndexName);
        await DropIndex<ObligationCalculation>(context, nameof(ObligationCalculation), IdentityIndexName);
    }
}
