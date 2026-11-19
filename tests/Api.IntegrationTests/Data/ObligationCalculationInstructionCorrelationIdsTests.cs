using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Data.Migrations;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.IntegrationTests.Data;

public class ObligationCalculationInstructionCorrelationIdsTests : IntegrationTestBase
{
    // Distinct years keep each active instruction unique under the active instruction index.
    private static int s_nextObligationYear = 2030;

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
