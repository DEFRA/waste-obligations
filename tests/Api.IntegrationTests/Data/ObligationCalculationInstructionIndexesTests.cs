using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Data.Migrations;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.IntegrationTests.Data;

public class ObligationCalculationInstructionIndexesTests : IntegrationTestBase
{
    [Fact]
    public async Task Migration_ShouldCreateAllJobIndexesAndSupportRepeatedUpAndDown()
    {
        var subject = new ObligationCalculationInstructionIndexes();
        var context = new MigrationContext(GetMongoDatabase(), null!, TestContext.Current.CancellationToken);
        const string activeIndex = "Active_ObligationYear_SubmitterId";
        const string retentionIndex = "CreatedAt_Terminal_Retention";
        const string workIndex = "Status_CreatedAt_Id";
        const string leaseRetentionIndex = "ExpiresAt_ProcessingLeaseRetention";
        var leases = GetMongoDatabase().GetCollection<BsonDocument>("_scheduled_job_processing_leases");
        try
        {
            await subject.UpAsync(context);
            await subject.UpAsync(context);
            var indexes = await (
                await ObligationCalculationInstructions.Indexes.ListAsync(TestContext.Current.CancellationToken)
            ).ToListAsync(TestContext.Current.CancellationToken);
            var active = indexes.Single(x => x["name"] == activeIndex);
            active["key"].Should().Be(new BsonDocument { { "obligationYear", 1 }, { "submitterId", 1 } });
            active["unique"].AsBoolean.Should().BeTrue();
            active["partialFilterExpression"]
                .Should()
                .Be(new BsonDocument("status", new BsonDocument("$in", new BsonArray { "Pending", "Running" })));
            var retention = indexes.Single(x => x["name"] == retentionIndex);
            retention["key"].Should().Be(new BsonDocument("createdAt", 1));
            retention["expireAfterSeconds"].ToInt64().Should().Be(31536000);
            retention["partialFilterExpression"]
                .Should()
                .Be(new BsonDocument("status", new BsonDocument("$in", new BsonArray { "Completed", "Failed" })));
            indexes
                .Single(x => x["name"] == workIndex)["key"]
                .Should()
                .Be(
                    new BsonDocument
                    {
                        { "status", 1 },
                        { "createdAt", 1 },
                        { "_id", 1 },
                    }
                );
            var leaseIndexes = await (
                await leases.Indexes.ListAsync(TestContext.Current.CancellationToken)
            ).ToListAsync(TestContext.Current.CancellationToken);
            leaseIndexes
                .Single(x => x["name"] == leaseRetentionIndex)["expireAfterSeconds"]
                .ToInt64()
                .Should()
                .Be(31536000);

            await subject.DownAsync(context);
            await subject.DownAsync(context);
            indexes = await (
                await ObligationCalculationInstructions.Indexes.ListAsync(TestContext.Current.CancellationToken)
            ).ToListAsync(TestContext.Current.CancellationToken);
            indexes.Should().OnlyContain(x => x["name"] == "_id_");
            leaseIndexes = await (await leases.Indexes.ListAsync(TestContext.Current.CancellationToken)).ToListAsync(
                TestContext.Current.CancellationToken
            );
            leaseIndexes.Should().OnlyContain(x => x["name"] == "_id_");
        }
        finally
        {
            await subject.UpAsync(context);
        }
    }

    [Fact]
    public async Task Retention_ShouldDeleteOldTerminalInstructionsAndPreserveActiveAndRecentInstructions()
    {
        var utcNow = DateTime.UtcNow;
        var oldCreatedAt = utcNow.AddDays(-366);
        var expired = new[]
        {
            ObligationCalculationInstructionStatus.Completed,
            ObligationCalculationInstructionStatus.Failed,
        }
            .Select(status =>
                ObligationCalculationInstructionFixture
                    .Default()
                    .With(x => x.Status, status)
                    .With(x => x.CreatedAt, oldCreatedAt)
                    .Create()
            )
            .ToArray();
        var pending = ObligationCalculationInstructionFixture.Default().With(x => x.CreatedAt, oldCreatedAt).Create();
        var running = ObligationCalculationInstructionFixture
            .Default()
            .With(x => x.Status, ObligationCalculationInstructionStatus.Running)
            .With(x => x.SubmitterId, Guid.NewGuid())
            .With(x => x.CreatedAt, oldCreatedAt)
            .Create();
        var recent = ObligationCalculationInstructionFixture
            .Default()
            .With(x => x.Status, ObligationCalculationInstructionStatus.Completed)
            .With(x => x.CreatedAt, utcNow)
            .Create();
        await ObligationCalculationInstructions.InsertManyAsync(
            [.. expired, pending, running, recent],
            cancellationToken: TestContext.Current.CancellationToken
        );

        await WaitForAsync(
            async () =>
            {
                var remaining = await ObligationCalculationInstructions
                    .Find(FilterDefinition<ObligationCalculationInstruction>.Empty)
                    .ToListAsync(TestContext.Current.CancellationToken);
                remaining.Select(x => x.Id).Should().BeEquivalentTo([pending.Id, running.Id, recent.Id]);
            },
            timeout: 90
        );
    }
}
