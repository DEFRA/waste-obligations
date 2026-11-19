using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Data.Migrations;
using Defra.WasteObligations.Api.ScheduledJobs;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.IntegrationTests.Data;

public class ObligationCalculationInstructionCorrelationIdsTests : IntegrationTestBase
{
    // Distinct years keep each active instruction unique under the active instruction index.
    private static int s_nextObligationYear = 2030;

    [Fact]
    public async Task WorkSource_ShouldWaitForBackfillAndReusePersistedCorrelationId()
    {
        var raw = GetMongoDatabase().GetCollection<BsonDocument>(ObligationCalculationInstruction.CollectionName);
        var instruction = Instruction("Pending");
        await raw.InsertOneAsync(instruction, cancellationToken: TestContext.Current.CancellationToken);
        var migrations = new MongoMigrationCompletion();
        var source = new ObligationCalculationWorkSource(GetMongoApplicationDatabase(), migrations);
        var selection = source.GetNext([], TestContext.Current.CancellationToken);

        selection.IsCompleted.Should().BeFalse();
        await new ObligationCalculationInstructionCorrelationIds().UpAsync(
            new MigrationContext(GetMongoDatabase(), null!, TestContext.Current.CancellationToken)
        );
        var persisted = (await CorrelationIds(raw))[instruction["_id"]];
        persisted.Should().MatchRegex("^[0-9a-f]{32}$");
        selection.IsCompleted.Should().BeFalse();
        migrations.Complete();

        var work = await selection;
        work.Should().NotBeNull();
        work.Payload.Id.Should().Be(instruction["_id"].AsObjectId);
        work.Payload.Status.Should().Be(ObligationCalculationInstructionStatus.Pending);
        work.CorrelationId.Should().Be(persisted);
        work.Payload.CorrelationId.Should().Be(persisted);
        var retry = await source.GetNext([], TestContext.Current.CancellationToken);
        retry.Should().NotBeNull();
        retry.CorrelationId.Should().Be(persisted);
    }

    [Fact]
    public async Task WorkSource_WhenMigrationsFail_ShouldNotSelectLegacyInstruction()
    {
        var raw = GetMongoDatabase().GetCollection<BsonDocument>(ObligationCalculationInstruction.CollectionName);
        await raw.InsertOneAsync(Instruction("Pending"), cancellationToken: TestContext.Current.CancellationToken);
        var migrations = new MongoMigrationCompletion();
        migrations.Fail(new InvalidOperationException("Backfill failed"));
        var source = new ObligationCalculationWorkSource(GetMongoApplicationDatabase(), migrations);

        var selection = () => source.GetNext([], TestContext.Current.CancellationToken);

        await selection.Should().ThrowAsync<InvalidOperationException>().WithMessage("Backfill failed");
    }

    [Fact]
    public async Task WorkSource_WhenStoppedBeforeMigrationsComplete_ShouldCancelSelection()
    {
        var source = new ObligationCalculationWorkSource(GetMongoApplicationDatabase(), new MongoMigrationCompletion());
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pending = source.GetNext([], cancellation.Token);

        await cancellation.CancelAsync();

        var selection = () => pending;
        await selection.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task UpAsync_ShouldPersistOneStableCorrelationIdForActiveLegacyInstructions()
    {
        var raw = GetMongoDatabase().GetCollection<BsonDocument>(ObligationCalculationInstruction.CollectionName);
        var missingPending = Instruction("Pending");
        var nullRunning = Instruction("Running", BsonNull.Value);
        var blankPending = Instruction("Pending", string.Empty);
        var existing = Instruction("Pending", "request-123");
        var missingCompleted = Instruction("Completed");
        await raw.InsertManyAsync(
            [missingPending, nullRunning, blankPending, existing, missingCompleted],
            cancellationToken: TestContext.Current.CancellationToken
        );
        var subject = new ObligationCalculationInstructionCorrelationIds();
        var context = new MigrationContext(GetMongoDatabase(), null!, TestContext.Current.CancellationToken);

        await subject.UpAsync(context);
        var first = await CorrelationIds(raw);
        await subject.UpAsync(context);
        var second = await CorrelationIds(raw);

        foreach (var instruction in new[] { missingPending, nullRunning, blankPending })
            first[instruction["_id"]].Should().MatchRegex("^[0-9a-f]{32}$");
        first[existing["_id"]].Should().Be("request-123");
        first[missingCompleted["_id"]].Should().BeNull();
        second.Should().BeEquivalentTo(first);
        (
            await ObligationCalculationInstructions
                .Find(x => x.Id == missingPending["_id"].AsObjectId)
                .SingleAsync(TestContext.Current.CancellationToken)
        )
            .CorrelationId.Should()
            .Be(first[missingPending["_id"]]);
    }

    private static BsonDocument Instruction(string status, BsonValue? correlationId = null)
    {
        var document = new BsonDocument
        {
            { "_id", ObjectId.GenerateNewId() },
            { "obligationYear", s_nextObligationYear++ },
            { "submitterId", BsonNull.Value },
            { "status", status },
            { "failureCount", 0 },
            { "createdAt", new DateTime(2026, 10, 30, 1, 0, 0, DateTimeKind.Utc) },
        };
        if (correlationId is not null)
            document["correlationId"] = correlationId;

        return document;
    }

    private static async Task<Dictionary<BsonValue, string?>> CorrelationIds(IMongoCollection<BsonDocument> raw) =>
        (
            await raw.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(TestContext.Current.CancellationToken)
        ).ToDictionary(
            x => x["_id"],
            x => x.TryGetValue("correlationId", out var value) && value.IsString ? value.AsString : null
        );
}
