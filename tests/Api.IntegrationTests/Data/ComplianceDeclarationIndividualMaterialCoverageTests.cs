using System.Text.Json;
using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Corrections;
using Defra.WasteObligations.Api.Data.Migrations;
using Defra.WasteObligations.Api.Schemas;
using Defra.WasteObligations.AuditEvents;
using Defra.WasteObligations.AuditEvents.Analytics;
using Defra.WasteObligations.AuditEvents.Data;
using Defra.WasteObligations.AuditEvents.Entities;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using MongoDB.Driver;
using NSubstitute;

namespace Defra.WasteObligations.Api.IntegrationTests.Data;

public class ComplianceDeclarationIndividualMaterialCoverageTests : IntegrationTestBase
{
    private const string CollectionName = "ComplianceDeclaration";
    private const string SourceVersion = "v1.3";
    private const string CheckpointsCollectionName = "_application_data_corrections";
    private static readonly DateTimeOffset s_correctedAt = new(2026, 10, 26, 16, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Submitted", 2023)]
    [InlineData("Accepted", 2025)]
    [InlineData("Cancelled", 2026)]
    public async Task Apply_WhenPercentageChanges_ShouldAtomicallyRecordCorrectionAndPreserveHistoricalEvents(
        string status,
        int year
    )
    {
        var collection = GetMongoDatabase().GetCollection<BsonDocument>(CollectionName);
        var original = Declaration(SourceVersion);
        original["status"] = status;
        original["obligationYear"] = year;
        var corrected = Declaration(SourceVersion);
        corrected["obligationCoveragePercentage"] = new BsonDecimal128(50m);
        var newer = Declaration("v1.4");
        await collection.InsertManyAsync(
            [original, corrected, newer],
            cancellationToken: TestContext.Current.CancellationToken
        );
        var historical = AuditEventFixture
            .ComplianceDeclaration(sequence: 100)
            .With(x => x.EntityId, original["_id"].AsObjectId.ToString())
            .With(x => x.Before, original.DeepClone().AsBsonDocument)
            .With(x => x.After, original.DeepClone().AsBsonDocument)
            .With(
                x => x.Dispatches,
                new Dictionary<string, AuditEventDispatch>
                {
                    ["analytics"] = new()
                    {
                        Status = AuditEventDispatchStatus.Failed,
                        Date = s_correctedAt.UtcDateTime,
                        AttemptCount = 1,
                        NextAttemptAt = new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    },
                }
            )
            .Create()
            .ToBsonDocument();
        var rawEvents = GetMongoDatabase().GetCollection<BsonDocument>(nameof(AuditEvent));
        await rawEvents.InsertOneAsync(historical, cancellationToken: TestContext.Current.CancellationToken);
        await AuditEventCounters.InsertOneAsync(
            new AuditEventCounter { Id = "audit_event", Sequence = 100 },
            cancellationToken: TestContext.Current.CancellationToken
        );
        var subject = CreateSubject();

        await subject.Apply(TestContext.Current.CancellationToken);
        await subject.Apply(TestContext.Current.CancellationToken);

        var expected = Corrected(original);
        (await Read(collection, original)).Should().Equal(expected);
        (await Read(collection, corrected)).Should().Equal(corrected);
        (await Read(collection, newer)).Should().Equal(newer);
        (await Read(rawEvents, historical)).Should().Equal(historical);
        var events = await AuditEvents
            .Find(x => x.EntityId == original["_id"].AsObjectId.ToString())
            .ToListAsync(TestContext.Current.CancellationToken);
        events.Should().HaveCount(2);
        var auditEvent = events.Single(x => x.EventId != historical["_id"].AsString);
        auditEvent.Sequence.Should().Be(101);
        auditEvent.Entity.Should().Be("compliance_declaration");
        auditEvent.Operation.Should().Be("update");
        auditEvent.EventType.Should().Be("submission.amended");
        auditEvent.Actor.Should().Be("service:waste-obligations");
        auditEvent.SchemaVersion.Should().Be(SourceVersion);
        auditEvent.Version.Should().Be(8);
        auditEvent.OccurredAt.Should().Be(s_correctedAt.UtcDateTime);
        auditEvent.RecordedAt.Should().Be(s_correctedAt.UtcDateTime);
        auditEvent.Before.Should().Equal(original);
        auditEvent.After.Should().Equal(expected);
        using var payload = JsonDocument.Parse(
            new JsonAnalyticsEventSerializer(new EmbeddedEntityJsonSchemaProvider()).Serialize(
                auditEvent.ToAnalyticsEvent()
            )
        );
        payload
            .RootElement.GetProperty("before")
            .GetProperty("obligationCoveragePercentage")
            .GetDecimal()
            .Should()
            .Be(90m);
        payload
            .RootElement.GetProperty("after")
            .GetProperty("obligationCoveragePercentage")
            .GetDecimal()
            .Should()
            .Be(50m);
        payload.RootElement.GetProperty("schemaVersion").GetString().Should().Be("compliance_declaration_v1.3");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("v1.0")]
    [InlineData("v1.1")]
    [InlineData("v1.2")]
    public async Task Apply_AfterPrecedingMigrations_ShouldAuditLegacyCorrection(string? version)
    {
        var collection = GetMongoDatabase().GetCollection<BsonDocument>(CollectionName);
        var original = Declaration(version);
        original.Remove("obligationCoveragePercentage");
        await collection.InsertOneAsync(original, cancellationToken: TestContext.Current.CancellationToken);
        var context = new MigrationContext(GetMongoDatabase(), null!, TestContext.Current.CancellationToken);
        await new ComplianceDeclarationUserLocale().UpAsync(context);
        await new ComplianceDeclarationObligationCoveragePercentage().UpAsync(context);
        await new ComplianceDeclarationObligationCoveragePercentagePrecision().UpAsync(context);
        await new ComplianceDeclarationBusinessCountry().UpAsync(context);
        var before = await Read(collection, original);

        await CreateSubject().Apply(TestContext.Current.CancellationToken);

        (await Read(collection, original)).Should().Equal(Corrected(before));
        var auditEvent = await AuditEvents
            .Find(x => x.EntityId == original["_id"].AsObjectId.ToString())
            .SingleAsync(TestContext.Current.CancellationToken);
        auditEvent.Before.Should().Equal(before);
        auditEvent.After.Should().Equal(Corrected(before));
    }

    [Fact]
    public async Task Run_WhenCorrectionFailsPartway_ShouldResumeWithoutDuplicatingEventsAndCheckpointOnlyAfterSuccess()
    {
        var collection = GetMongoDatabase().GetCollection<BsonDocument>(CollectionName);
        var first = Declaration(SourceVersion);
        var invalid = Declaration(SourceVersion);
        invalid["obligations"] = new BsonArray { new BsonDocument() };
        await collection.InsertManyAsync([first, invalid], cancellationToken: TestContext.Current.CancellationToken);
        var correction = CreateSubject();
        var checkpoints = GetMongoDatabase().GetCollection<BsonDocument>(CheckpointsCollectionName);
        await checkpoints.DeleteOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", correction.Id),
            TestContext.Current.CancellationToken
        );
        var subject = new ApplicationDataCorrectionRunner(
            GetMongoApplicationDatabase(),
            [correction],
            new FakeTimeProvider(s_correctedAt),
            Substitute.For<ILogger<ApplicationDataCorrectionRunner>>()
        );

        var action = () => subject.Run(TestContext.Current.CancellationToken);
        await action
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{invalid["_id"].AsObjectId}*")
            .WithInnerException<KeyNotFoundException>();

        (await Read(collection, first)).Should().Equal(Corrected(first));
        (await Read(collection, invalid)).Should().Equal(invalid);
        (
            await checkpoints
                .Find(Builders<BsonDocument>.Filter.Eq("_id", correction.Id))
                .AnyAsync(TestContext.Current.CancellationToken)
        )
            .Should()
            .BeFalse();
        await collection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", invalid["_id"]),
            Builders<BsonDocument>.Update.Set("obligations", first["obligations"]),
            cancellationToken: TestContext.Current.CancellationToken
        );
        await subject.Run(TestContext.Current.CancellationToken);
        await subject.Run(TestContext.Current.CancellationToken);

        (await Read(collection, first)).Should().Equal(Corrected(first));
        var repaired = invalid.DeepClone().AsBsonDocument;
        repaired["obligations"] = first["obligations"];
        (await Read(collection, invalid)).Should().Equal(Corrected(repaired));
        var events = await AuditEvents
            .Find(x => x.EventType == "submission.amended")
            .ToListAsync(TestContext.Current.CancellationToken);
        events.Should().HaveCount(2);
        events.Select(x => x.EntityId).Should().OnlyHaveUniqueItems();
        var checkpoint = await checkpoints
            .Find(Builders<BsonDocument>.Filter.Eq("_id", correction.Id))
            .SingleAsync(TestContext.Current.CancellationToken);
        checkpoint["completedAt"].ToUniversalTime().Should().Be(s_correctedAt.UtcDateTime);
        var later = Declaration(SourceVersion);
        await collection.InsertOneAsync(later, cancellationToken: TestContext.Current.CancellationToken);

        await subject.Run(TestContext.Current.CancellationToken);

        (await Read(collection, later)).Should().Equal(later);
    }

    [Fact]
    public async Task Apply_WhenAuditWriteFails_ShouldRollBackDeclarationAndAllowRetry()
    {
        var collection = GetMongoDatabase().GetCollection<BsonDocument>(CollectionName);
        var original = Declaration(SourceVersion);
        await collection.InsertOneAsync(original, cancellationToken: TestContext.Current.CancellationToken);
        var failingEvents = Substitute.For<IAuditEventService>();
        failingEvents
            .RecordEvent(Arg.Any<IClientSessionHandle>(), Arg.Any<AuditEventRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Audit storage unavailable")));
        var action = () => CreateSubject(failingEvents).Apply(TestContext.Current.CancellationToken);

        await action
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{original["_id"].AsObjectId}*")
            .WithInnerException<InvalidOperationException>()
            .WithMessage("Audit storage unavailable");

        (await Read(collection, original)).Should().Equal(original);
        (
            await AuditEvents
                .Find(x => x.EntityId == original["_id"].AsObjectId.ToString())
                .AnyAsync(TestContext.Current.CancellationToken)
        )
            .Should()
            .BeFalse();
        await CreateSubject().Apply(TestContext.Current.CancellationToken);
        (await Read(collection, original)).Should().Equal(Corrected(original));
        (
            await AuditEvents
                .Find(x => x.EntityId == original["_id"].AsObjectId.ToString())
                .CountDocumentsAsync(TestContext.Current.CancellationToken)
        )
            .Should()
            .Be(1);
    }

    [Fact]
    public async Task Apply_WhenVersionIsMissingAndZeroMetAndNoDataYetRowsExist_ShouldUseCurrentFormulaAndStartVersion()
    {
        var collection = GetMongoDatabase().GetCollection<BsonDocument>(CollectionName);
        var original = Declaration(SourceVersion);
        original.Remove("version");
        original["obligations"] = new BsonArray
        {
            Obligation(0, 0, "Met"),
            Obligation(0, 10, "NotMet"),
            Obligation(90, 90, "NoDataYet"),
        };
        await collection.InsertOneAsync(original, cancellationToken: TestContext.Current.CancellationToken);

        await CreateSubject().Apply(TestContext.Current.CancellationToken);

        var expected = Corrected(original);
        expected["version"] = 2;
        (await Read(collection, original)).Should().Equal(expected);
        var auditEvent = await AuditEvents
            .Find(x => x.EntityId == original["_id"].AsObjectId.ToString())
            .SingleAsync(TestContext.Current.CancellationToken);
        auditEvent.Version.Should().Be(2);
        auditEvent.Before.Should().Equal(original);
        auditEvent.After.Should().Equal(expected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Apply_WhenSnapshotChangesDuringTransaction_ShouldRetryAndAuditTheCurrentSnapshot(
        bool changeObligations
    )
    {
        var collection = GetMongoDatabase().GetCollection<BsonDocument>(CollectionName);
        var original = Declaration(SourceVersion);
        await collection.InsertOneAsync(original, cancellationToken: TestContext.Current.CancellationToken);
        var applicationDatabase = GetMongoApplicationDatabase();
        var applicationCollection = applicationDatabase.GetCollection<BsonDocument>(CollectionName);
        var intercepted = Substitute.For<IMongoCollection<BsonDocument>>();
        intercepted.Indexes.Returns(applicationCollection.Indexes);
        intercepted.DocumentSerializer.Returns(applicationCollection.DocumentSerializer);
        intercepted.Settings.Returns(applicationCollection.Settings);
        intercepted
            .FindAsync(
                Arg.Any<FilterDefinition<BsonDocument>>(),
                Arg.Any<FindOptions<BsonDocument, BsonDocument>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(x =>
                applicationCollection.FindAsync(
                    x.Arg<FilterDefinition<BsonDocument>>(),
                    x.Arg<FindOptions<BsonDocument, BsonDocument>>(),
                    x.Arg<CancellationToken>()
                )
            );
        intercepted
            .FindAsync(
                Arg.Any<IClientSessionHandle>(),
                Arg.Any<FilterDefinition<BsonDocument>>(),
                Arg.Any<FindOptions<BsonDocument, BsonDocument>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(x =>
                applicationCollection.FindAsync(
                    x.Arg<IClientSessionHandle>(),
                    x.Arg<FilterDefinition<BsonDocument>>(),
                    x.Arg<FindOptions<BsonDocument, BsonDocument>>(),
                    x.Arg<CancellationToken>()
                )
            );
        BsonDocument? concurrent = null;
        intercepted
            .UpdateOneAsync(
                Arg.Any<IClientSessionHandle>(),
                Arg.Any<FilterDefinition<BsonDocument>>(),
                Arg.Any<UpdateDefinition<BsonDocument>>(),
                Arg.Any<UpdateOptions>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(async x =>
            {
                if (concurrent is null)
                {
                    var update = Builders<BsonDocument>
                        .Update.Set("version", 8)
                        .Set("unrelated.concurrent", "retained");
                    if (changeObligations)
                        update = update.Set(
                            "obligations",
                            new BsonArray { Obligation(0, 90, "NotMet"), Obligation(0, 10, "NotMet") }
                        );
                    await collection.UpdateOneAsync(
                        Builders<BsonDocument>.Filter.Eq("_id", original["_id"]),
                        update,
                        cancellationToken: TestContext.Current.CancellationToken
                    );
                    concurrent = await Read(collection, original);
                }

                return await applicationCollection.UpdateOneAsync(
                    x.Arg<IClientSessionHandle>(),
                    x.Arg<FilterDefinition<BsonDocument>>(),
                    x.Arg<UpdateDefinition<BsonDocument>>(),
                    x.Arg<UpdateOptions>(),
                    x.Arg<CancellationToken>()
                );
            });
        var database = Substitute.For<IMongoDatabase>();
        database.GetCollection<BsonDocument>(CollectionName, Arg.Any<MongoCollectionSettings>()).Returns(intercepted);
        var timeProvider = new FakeTimeProvider(s_correctedAt);
        var context = new MongoDbContext(
            applicationDatabase,
            Options.Create(new MongoDbOptions()),
            Substitute.For<ILogger<MongoDbContext>>()
        );
        var subject = new ComplianceDeclarationCoverageCorrection(
            database,
            context,
            new AuditEventService(
                new AuditEventDbContext(applicationDatabase),
                timeProvider,
                new UlidEventIdGenerator()
            ),
            timeProvider
        );

        await subject.Apply(TestContext.Current.CancellationToken);

        concurrent.Should().NotBeNull();
        var expected = Corrected(concurrent);
        expected["version"] = 9;
        expected["obligationCoveragePercentage"] = new BsonDecimal128(changeObligations ? 0m : 50m);
        (await Read(collection, original)).Should().Equal(expected);
        var auditEvent = await AuditEvents
            .Find(x => x.EntityId == original["_id"].AsObjectId.ToString())
            .SingleAsync(TestContext.Current.CancellationToken);
        auditEvent.Before.Should().Equal(concurrent);
        auditEvent.After.Should().Equal(expected);
    }

    private static ComplianceDeclarationCoverageCorrection CreateSubject(IAuditEventService? auditEventService = null)
    {
        var database = GetMongoApplicationDatabase();
        var timeProvider = new FakeTimeProvider(s_correctedAt);
        var context = new MongoDbContext(
            database,
            Options.Create(new MongoDbOptions()),
            Substitute.For<ILogger<MongoDbContext>>()
        );

        return new ComplianceDeclarationCoverageCorrection(
            database,
            context,
            auditEventService
                ?? new AuditEventService(new AuditEventDbContext(database), timeProvider, new UlidEventIdGenerator()),
            timeProvider
        );
    }

    private static BsonDocument Corrected(BsonDocument original)
    {
        var expected = original.DeepClone().AsBsonDocument;
        expected["obligationCoveragePercentage"] = new BsonDecimal128(50m);
        expected["version"] = 8;
        expected["updated"] = s_correctedAt.UtcDateTime;

        return expected;
    }

    private static async Task<BsonDocument> Read(IMongoCollection<BsonDocument> collection, BsonDocument document)
    {
        return await collection
            .Find(Builders<BsonDocument>.Filter.Eq("_id", document["_id"]))
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private static BsonDocument Declaration(string? schemaVersion)
    {
        var document = ComplianceDeclarationFixture.DirectProducer().With(x => x.Version, 7).Create().ToBsonDocument();
        document["created"] = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        document["updated"] = new DateTime(2023, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        document["unrelated"] = new BsonDocument("nested", 42);
        document["obligationCoveragePercentage"] = new BsonDecimal128(90m);
        document["obligations"] = new BsonArray { Obligation(90, 90, "Met"), Obligation(0, 10, "NotMet") };
        if (schemaVersion is not null)
            document["schemaVersion"] = schemaVersion;
        else
            document.Remove("schemaVersion");

        return document;
    }

    private static BsonDocument Obligation(int accepted, int obligated, string status) =>
        new()
        {
            ["material"] = "Plastic",
            ["status"] = status,
            ["tonnages"] = new BsonDocument { ["accepted"] = accepted, ["obligated"] = obligated },
        };
}
