using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Migrations;
using Defra.WasteObligations.AuditEvents.Entities;
using Defra.WasteObligations.Testing;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using MongoDB.Bson;
using MongoDB.Driver;
using NSubstitute;

namespace Defra.WasteObligations.Api.IntegrationTests.Data;

public class ComplianceDeclarationIndividualMaterialCoverageTests : IntegrationTestBase
{
    private const string CollectionName = "ComplianceDeclaration";
    private const string SourceVersion = "v1.3";
    private const string TargetVersion = "v2.0";

    [Theory]
    [InlineData("Submitted", 2023)]
    [InlineData("Accepted", 2025)]
    [InlineData("Cancelled", 2026)]
    public async Task Up_WhenEligible_ShouldCorrectOnlyPercentageAndSchemaAndLeaveAuditEventsUnchanged(
        string status,
        int year
    )
    {
        var collection = GetMongoDatabase().GetCollection<BsonDocument>(CollectionName);
        var original = Declaration(SourceVersion);
        original["status"] = status;
        original["obligationYear"] = year;
        var target = Declaration(TargetVersion);
        var newer = Declaration("v3.0");
        await collection.InsertManyAsync(
            [original, target, newer],
            cancellationToken: TestContext.Current.CancellationToken
        );
        var auditEvents = GetMongoDatabase().GetCollection<BsonDocument>("AuditEvent");
        var auditEvent = AuditEventFixture
            .ComplianceDeclaration()
            .With(x => x.EntityId, original["_id"].AsObjectId.ToString())
            .With(x => x.SchemaVersion, SourceVersion)
            .With(x => x.Before, original.DeepClone().AsBsonDocument)
            .With(x => x.After, original.DeepClone().AsBsonDocument)
            .With(
                x => x.Dispatches,
                new Dictionary<string, AuditEventDispatch>
                {
                    ["analytics"] = new()
                    {
                        Status = AuditEventDispatchStatus.Failed,
                        Date = new DateTime(2023, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                        Message = "Retry required",
                        AttemptCount = 1,
                        NextAttemptAt = new DateTime(2099, 2, 1, 0, 5, 0, DateTimeKind.Utc),
                    },
                }
            )
            .Create()
            .ToBsonDocument();
        await auditEvents.InsertOneAsync(auditEvent, cancellationToken: TestContext.Current.CancellationToken);
        var subject = new ComplianceDeclarationIndividualMaterialCoverage();

        await subject.UpAsync(Context());
        await subject.UpAsync(Context());

        var expected = original.DeepClone().AsBsonDocument;
        expected["schemaVersion"] = TargetVersion;
        expected["obligationCoveragePercentage"] = new BsonDecimal128(50m);
        (await Read(collection, original)).Should().Equal(expected);
        (await Read(collection, target)).Should().Equal(target);
        (await Read(collection, newer)).Should().Equal(newer);
        (await Read(auditEvents, auditEvent)).Should().Equal(auditEvent);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("v1.0")]
    [InlineData("v1.1")]
    [InlineData("v1.2")]
    public async Task Up_AfterPrecedingMigrations_ShouldCorrectLegacyDocuments(string? version)
    {
        var collection = GetMongoDatabase().GetCollection<BsonDocument>(CollectionName);
        var document = Declaration(version);
        document.Remove("obligationCoveragePercentage");
        await collection.InsertOneAsync(document, cancellationToken: TestContext.Current.CancellationToken);
        var legacyContext = new MigrationContext(GetMongoDatabase(), null!, TestContext.Current.CancellationToken);
        await new ComplianceDeclarationUserLocale().UpAsync(legacyContext);
        await new ComplianceDeclarationObligationCoveragePercentage().UpAsync(legacyContext);
        await new ComplianceDeclarationObligationCoveragePercentagePrecision().UpAsync(legacyContext);
        await new ComplianceDeclarationBusinessCountry().UpAsync(legacyContext);

        await new ComplianceDeclarationIndividualMaterialCoverage().UpAsync(Context());

        var result = await Read(collection, document);
        result["schemaVersion"].AsString.Should().Be(TargetVersion);
        result["obligationCoveragePercentage"].AsDecimal128.Should().Be(new Decimal128(50m));
        result["obligations"].Should().Be(document["obligations"]);
        result["version"].Should().Be(document["version"]);
    }

    [Fact]
    public async Task Up_WhenAnInvalidSnapshotInterruptsProgress_ShouldRetainCompletedUpdatesAndResumeAfterRepair()
    {
        var collection = GetMongoDatabase().GetCollection<BsonDocument>(CollectionName);
        var first = Declaration(SourceVersion);
        var invalid = Declaration(SourceVersion);
        invalid["obligations"] = new BsonArray { new BsonDocument() };
        await collection.InsertManyAsync([first, invalid], cancellationToken: TestContext.Current.CancellationToken);
        var subject = new ComplianceDeclarationIndividualMaterialCoverage();

        var action = () => subject.UpAsync(Context());
        await action.Should().ThrowAsync<KeyNotFoundException>();

        (await Read(collection, first))["schemaVersion"].AsString.Should().Be(TargetVersion);
        (await Read(collection, invalid))["schemaVersion"].AsString.Should().Be(SourceVersion);
        await collection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", invalid["_id"]),
            Builders<BsonDocument>.Update.Set("obligations", first["obligations"]),
            cancellationToken: TestContext.Current.CancellationToken
        );
        await subject.UpAsync(Context());
        await subject.UpAsync(Context());

        (await Read(collection, first))["obligationCoveragePercentage"].AsDecimal128.Should().Be(new Decimal128(50m));
        (await Read(collection, invalid))["schemaVersion"].AsString.Should().Be(TargetVersion);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Up_WhenSnapshotOrVersionChangesBeforeWrite_ShouldFailWithoutOverwritingAndRetryFromTheCurrentSnapshot(
        bool changeObligations
    )
    {
        var collection = GetMongoDatabase().GetCollection<BsonDocument>(CollectionName);
        var document = Declaration(SourceVersion);
        await collection.InsertOneAsync(document, cancellationToken: TestContext.Current.CancellationToken);
        var changed = false;
        var applicationCollection = GetMongoApplicationDatabase().GetCollection<BsonDocument>(CollectionName);
        var interceptedCollection = Substitute.For<IMongoCollection<BsonDocument>>();
        interceptedCollection.Indexes.Returns(applicationCollection.Indexes);
        interceptedCollection.DocumentSerializer.Returns(applicationCollection.DocumentSerializer);
        interceptedCollection.Settings.Returns(applicationCollection.Settings);
        interceptedCollection
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
        interceptedCollection
            .UpdateOneAsync(
                Arg.Any<FilterDefinition<BsonDocument>>(),
                Arg.Any<UpdateDefinition<BsonDocument>>(),
                Arg.Any<UpdateOptions>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(async x =>
            {
                changed = true;
                var update = Builders<BsonDocument>.Update.Set("unrelated.concurrent", "retained");
                update = changeObligations
                    ? update.Set(
                        "obligations",
                        new BsonArray { Obligation(0, 90, "NotMet"), Obligation(0, 10, "NotMet") }
                    )
                    : update.Set("version", 8);
                await collection.UpdateOneAsync(
                    Builders<BsonDocument>.Filter.Eq("_id", document["_id"]),
                    update,
                    cancellationToken: TestContext.Current.CancellationToken
                );

                return await applicationCollection.UpdateOneAsync(
                    x.Arg<FilterDefinition<BsonDocument>>(),
                    x.Arg<UpdateDefinition<BsonDocument>>(),
                    x.Arg<UpdateOptions>(),
                    x.Arg<CancellationToken>()
                );
            });
        var database = Substitute.For<IMongoDatabase>();
        database
            .GetCollection<BsonDocument>(CollectionName, Arg.Any<MongoCollectionSettings>())
            .Returns(interceptedCollection);
        var context = new MigrationContext(database, null!, TestContext.Current.CancellationToken);
        var subject = new ComplianceDeclarationIndividualMaterialCoverage();

        var action = () => subject.UpAsync(context);
        await action
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*changed during coverage migration*");

        changed.Should().BeTrue();
        var afterConflict = await Read(collection, document);
        var expectedConflict = document.DeepClone().AsBsonDocument;
        expectedConflict["unrelated"]["concurrent"] = "retained";
        if (changeObligations)
            expectedConflict["obligations"] = new BsonArray
            {
                Obligation(0, 90, "NotMet"),
                Obligation(0, 10, "NotMet"),
            };
        else
            expectedConflict["version"] = 8;
        afterConflict.Should().Equal(expectedConflict);
        await subject.UpAsync(Context());
        var expected = afterConflict.DeepClone().AsBsonDocument;
        expected["schemaVersion"] = TargetVersion;
        expected["obligationCoveragePercentage"] = new BsonDecimal128(changeObligations ? 0m : 50m);
        (await Read(collection, document)).Should().Equal(expected);
    }

    [Fact]
    public async Task Up_WhenConcurrencyVersionIsMissingAndLowTonnageMetAndNoDataYetRowsExist_ShouldPreserveTheSnapshot()
    {
        var collection = GetMongoDatabase().GetCollection<BsonDocument>(CollectionName);
        var document = Declaration(SourceVersion);
        document.Remove("version");
        document["obligations"] = new BsonArray
        {
            Obligation(0, 0, "Met"),
            Obligation(0, 10, "NotMet"),
            Obligation(90, 90, "NoDataYet"),
        };
        await collection.InsertOneAsync(document, cancellationToken: TestContext.Current.CancellationToken);
        var subject = new ComplianceDeclarationIndividualMaterialCoverage();

        await subject.UpAsync(Context());
        await subject.UpAsync(Context());

        var expected = document.DeepClone().AsBsonDocument;
        expected["schemaVersion"] = TargetVersion;
        expected["obligationCoveragePercentage"] = new BsonDecimal128(50m);
        (await Read(collection, document)).Should().Equal(expected);
    }

    [Fact]
    public async Task Down_ShouldRefuseToInventPreviousValuesAndLeaveDataUnchanged()
    {
        var collection = GetMongoDatabase().GetCollection<BsonDocument>(CollectionName);
        var document = Declaration(TargetVersion);
        await collection.InsertOneAsync(document, cancellationToken: TestContext.Current.CancellationToken);

        var action = () => new ComplianceDeclarationIndividualMaterialCoverage().DownAsync(Context());

        await action
            .Should()
            .ThrowAsync<NotSupportedException>()
            .WithMessage("*original stored percentage was not retained*");
        (await Read(collection, document)).Should().Equal(document);
    }

    private static MigrationContext Context() =>
        new(GetMongoApplicationDatabase(), null!, TestContext.Current.CancellationToken);

    private static async Task<BsonDocument> Read(IMongoCollection<BsonDocument> collection, BsonDocument document)
    {
        return await collection
            .Find(Builders<BsonDocument>.Filter.Eq("_id", document["_id"]))
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private static BsonDocument Declaration(string? schemaVersion)
    {
        var document = new BsonDocument
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["version"] = 7,
            ["created"] = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ["updated"] = new DateTime(2023, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            ["status"] = "Accepted",
            ["obligationYear"] = 2023,
            ["submitterName"] = "Original submitter",
            ["organisation"] = new BsonDocument("unrelated", "preserved"),
            ["unrelated"] = new BsonDocument("nested", 42),
            ["audit"] = new BsonArray
            {
                new BsonDocument
                {
                    ["action"] = "Submitted",
                    ["user"] = new BsonDocument { ["id"] = "original-user", ["locale"] = "en" },
                    ["timestamp"] = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                },
            },
            ["obligationCoveragePercentage"] = new BsonDecimal128(90m),
            ["obligations"] = new BsonArray { Obligation(90, 90, "Met"), Obligation(0, 10, "NotMet") },
        };
        if (schemaVersion is not null)
            document["schemaVersion"] = schemaVersion;

        return document;
    }

    private static BsonDocument Obligation(int accepted, int obligated, string status) =>
        new()
        {
            ["status"] = status,
            ["tonnages"] = new BsonDocument { ["accepted"] = accepted, ["obligated"] = obligated },
        };
}
