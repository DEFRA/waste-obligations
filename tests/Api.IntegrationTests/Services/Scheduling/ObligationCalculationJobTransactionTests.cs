using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.ScheduledJobs;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using NSubstitute;

namespace Defra.WasteObligations.Api.IntegrationTests.Services.Scheduling;

public class ObligationCalculationJobTransactionTests : IntegrationTestBase
{
    [Fact]
    public async Task ProductionJob_WhenTransactionIsAborted_ShouldNotPersistInstructionOrOccurrence()
    {
        var database = GetMongoApplicationDatabase();
        var context = new MongoDbContext(
            database,
            Options.Create(new MongoDbOptions()),
            NullLogger<MongoDbContext>.Instance
        );
        var provider = Substitute.For<ICurrentObligationYearProvider>();
        provider.GetCurrentObligationYear().Returns(2026);
        var job = new ObligationCalculationJob(
            provider,
            new ObligationCalculationInstructionService(database, TimeProvider.System),
            NullLogger<ObligationCalculationJob>.Instance
        );
        var markerId = $"rollback-{Guid.NewGuid():N}";
        var markers = database.GetCollection<BsonDocument>("_scheduled_job_state");
        var markerReader = GetMongoDatabase().GetCollection<BsonDocument>("_scheduled_job_state");

        try
        {
            Func<Task> handoff = () =>
                context.ExecuteTransaction<bool>(
                    async (session, token) =>
                    {
                        await job.Run(session, token);
                        await markers.InsertOneAsync(
                            session,
                            new BsonDocument { { "_id", markerId }, { "lastStartedAt", DateTime.UtcNow } },
                            cancellationToken: token
                        );
                        throw new InvalidOperationException("Interrupt handoff before commit");
                    },
                    "test production obligation handoff rollback",
                    TestContext.Current.CancellationToken
                );

            await handoff.Should().ThrowAsync<InvalidOperationException>();
            (
                await ObligationCalculationInstructions.CountDocumentsAsync(
                    FilterDefinition<ObligationCalculationInstruction>.Empty,
                    cancellationToken: TestContext.Current.CancellationToken
                )
            )
                .Should()
                .Be(0);
            (
                await markerReader.CountDocumentsAsync(
                    new BsonDocument("_id", markerId),
                    cancellationToken: TestContext.Current.CancellationToken
                )
            )
                .Should()
                .Be(0);
        }
        finally
        {
            await markerReader.DeleteOneAsync(new BsonDocument("_id", markerId), TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task ProductionJob_WhenHandoffIsCancelled_ShouldRollBackInstructionAndOccurrence()
    {
        var database = GetMongoApplicationDatabase();
        var context = new MongoDbContext(
            database,
            Options.Create(new MongoDbOptions()),
            NullLogger<MongoDbContext>.Instance
        );
        var provider = Substitute.For<ICurrentObligationYearProvider>();
        provider.GetCurrentObligationYear().Returns(2026);
        var job = new ObligationCalculationJob(
            provider,
            new ObligationCalculationInstructionService(database, TimeProvider.System),
            NullLogger<ObligationCalculationJob>.Instance
        );
        var markerId = $"cancel-{Guid.NewGuid():N}";
        var markers = database.GetCollection<BsonDocument>("_scheduled_job_state");
        var markerReader = GetMongoDatabase().GetCollection<BsonDocument>("_scheduled_job_state");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        try
        {
            Func<Task> handoff = () =>
                context.ExecuteTransaction<bool>(
                    async (session, token) =>
                    {
                        await job.Run(session, token);
                        await markers.InsertOneAsync(
                            session,
                            new BsonDocument { { "_id", markerId }, { "lastStartedAt", DateTime.UtcNow } },
                            cancellationToken: token
                        );
                        await cancellation.CancelAsync();
                        token.ThrowIfCancellationRequested();
                        return true;
                    },
                    "test cancelled obligation handoff",
                    cancellation.Token
                );
            await handoff.Should().ThrowAsync<OperationCanceledException>();
            (
                await ObligationCalculationInstructions.CountDocumentsAsync(
                    FilterDefinition<ObligationCalculationInstruction>.Empty,
                    cancellationToken: TestContext.Current.CancellationToken
                )
            )
                .Should()
                .Be(0);
            (
                await markerReader.CountDocumentsAsync(
                    new BsonDocument("_id", markerId),
                    cancellationToken: TestContext.Current.CancellationToken
                )
            )
                .Should()
                .Be(0);
        }
        finally
        {
            await markerReader.DeleteOneAsync(new BsonDocument("_id", markerId), TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task FirstHandoff_WhenStateCollectionDoesNotExist_ShouldCreateItWithinTransaction()
    {
        var client = GetMongoApplicationDatabase().Client;
        var databaseName = $"scheduled_first_handoff_{Guid.NewGuid():N}";
        var database = client.GetDatabase(databaseName);
        var fixtureDatabase = GetMongoDatabase().Client.GetDatabase(databaseName);
        try
        {
            await database.CreateCollectionAsync(
                ObligationCalculationInstruction.CollectionName,
                cancellationToken: TestContext.Current.CancellationToken
            );
            var context = new MongoDbContext(
                database,
                Options.Create(new MongoDbOptions()),
                NullLogger<MongoDbContext>.Instance
            );
            var provider = Substitute.For<ICurrentObligationYearProvider>();
            provider.GetCurrentObligationYear().Returns(2026);
            var job = new ObligationCalculationJob(
                provider,
                new ObligationCalculationInstructionService(database, TimeProvider.System),
                NullLogger<ObligationCalculationJob>.Instance
            );
            var markers = database.GetCollection<BsonDocument>("_scheduled_job_state");
            var existing = await (
                await fixtureDatabase.ListCollectionNamesAsync(cancellationToken: TestContext.Current.CancellationToken)
            ).ToListAsync(TestContext.Current.CancellationToken);
            existing.Should().NotContain("_scheduled_job_state");
            await context.ExecuteTransaction(
                async (session, token) =>
                {
                    await job.Run(session, token);
                    await markers.UpdateOneAsync(
                        session,
                        new BsonDocument("_id", "ObligationCalculation"),
                        new BsonDocument("$set", new BsonDocument("lastStartedAt", DateTime.UtcNow)),
                        new UpdateOptions { IsUpsert = true },
                        token
                    );
                    return true;
                },
                "test first scheduled handoff",
                TestContext.Current.CancellationToken
            );
            (
                await fixtureDatabase
                    .GetCollection<BsonDocument>("_scheduled_job_state")
                    .CountDocumentsAsync(
                        new BsonDocument("_id", "ObligationCalculation"),
                        cancellationToken: TestContext.Current.CancellationToken
                    )
            )
                .Should()
                .Be(1);
            (
                await fixtureDatabase
                    .GetCollection<BsonDocument>(ObligationCalculationInstruction.CollectionName)
                    .CountDocumentsAsync(
                        FilterDefinition<BsonDocument>.Empty,
                        cancellationToken: TestContext.Current.CancellationToken
                    )
            )
                .Should()
                .Be(1);
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName, TestContext.Current.CancellationToken);
        }
    }
}
