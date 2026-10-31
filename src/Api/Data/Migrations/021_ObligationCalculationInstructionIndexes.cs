using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(ObligationCalculationInstruction.CollectionName, MigrationDirection.Both)]
public class ObligationCalculationInstructionIndexes : MongoMigration
{
    private const string ActiveIndexName = "Active_ObligationYear_SubmitterId";
    private const string RetentionIndexName = "CreatedAt_Terminal_Retention";
    private const string ProcessingLeaseRetentionIndexName = "ExpiresAt_ProcessingLeaseRetention";
    private const string ProcessingLeaseCollection = "_scheduled_job_processing_leases";
    private const string WorkIndexName = "Status_CreatedAt_Id";

    public override MigrationVersion Version => new(1, 0, 20);

    public override string Name => "021 - Obligation calculation instruction indexes";

    public override async Task UpAsync(MigrationContext context)
    {
        var collection = context.Database.GetCollection<BsonDocument>(ObligationCalculationInstruction.CollectionName);
        await collection.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<BsonDocument>(
                    new BsonDocument { { "obligationYear", 1 }, { "submitterId", 1 } },
                    new CreateIndexOptions<BsonDocument>
                    {
                        Name = ActiveIndexName,
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
                new CreateIndexModel<BsonDocument>(
                    new BsonDocument("createdAt", 1),
                    new CreateIndexOptions<BsonDocument>
                    {
                        Name = RetentionIndexName,
                        ExpireAfter = TimeSpan.FromDays(365),
                        PartialFilterExpression = new BsonDocument(
                            "status",
                            new BsonDocument(
                                "$in",
                                new BsonArray
                                {
                                    nameof(ObligationCalculationInstructionStatus.Completed),
                                    nameof(ObligationCalculationInstructionStatus.Failed),
                                }
                            )
                        ),
                    }
                ),
                new CreateIndexModel<BsonDocument>(
                    new BsonDocument
                    {
                        { "status", 1 },
                        { "createdAt", 1 },
                        { "_id", 1 },
                    },
                    new CreateIndexOptions { Name = WorkIndexName }
                ),
            ],
            cancellationToken: context.CancellationToken
        );
        await context
            .Database.GetCollection<BsonDocument>(ProcessingLeaseCollection)
            .Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    new BsonDocument("expiresAt", 1),
                    new CreateIndexOptions
                    {
                        Name = ProcessingLeaseRetentionIndexName,
                        ExpireAfter = TimeSpan.FromDays(365),
                    }
                ),
                cancellationToken: context.CancellationToken
            );
    }

    public override async Task DownAsync(MigrationContext context)
    {
        await DropIndex<BackgroundWorkerLease>(context, ProcessingLeaseCollection, ProcessingLeaseRetentionIndexName);
        await DropIndex<ObligationCalculationInstruction>(
            context,
            ObligationCalculationInstruction.CollectionName,
            WorkIndexName
        );
        await DropIndex<ObligationCalculationInstruction>(
            context,
            ObligationCalculationInstruction.CollectionName,
            RetentionIndexName
        );
        await DropIndex<ObligationCalculationInstruction>(
            context,
            ObligationCalculationInstruction.CollectionName,
            ActiveIndexName
        );
    }
}
