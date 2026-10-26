using System.Text.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Corrections;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Schemas;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Utils.Logging;
using Defra.WasteObligations.Api.Utils.Metrics;
using Defra.WasteObligations.AuditEvents;
using Defra.WasteObligations.AuditEvents.Analytics;
using Defra.WasteObligations.AuditEvents.Data;
using Defra.WasteObligations.AuditEvents.Entities;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Microsoft.AspNetCore.HeaderPropagation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using MongoDB.Driver;
using NSubstitute;

namespace Defra.WasteObligations.Api.IntegrationTests.Data;

public class ComplianceDeclarationIndividualMaterialCoverageTests : IntegrationTestBase
{
    private const string CheckpointsCollectionName = "_application_data_corrections";
    private static readonly DateTimeOffset s_correctedAt = new(2026, 10, 26, 16, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(ComplianceDeclarationStatus.Submitted, 2023)]
    [InlineData(ComplianceDeclarationStatus.Accepted, 2025)]
    [InlineData(ComplianceDeclarationStatus.Cancelled, 2026)]
    public async Task Apply_ShouldUpdateChangedEntitiesThroughTheServiceAndPreserveHistoricalEvents(
        ComplianceDeclarationStatus status,
        int year
    )
    {
        var original = Declaration() with { Status = status, ObligationYear = year };
        var unchanged = Declaration() with { ObligationCoveragePercentage = 50m };
        await ComplianceDeclarations.InsertManyAsync(
            [original, unchanged],
            cancellationToken: TestContext.Current.CancellationToken
        );
        var historical = AuditEventFixture
            .ComplianceDeclaration(sequence: 100)
            .With(x => x.EntityId, original.Id.ToString())
            .With(x => x.Before, original.ToBsonDocument())
            .With(x => x.After, original.ToBsonDocument())
            .Create();
        await AuditEvents.InsertOneAsync(historical, cancellationToken: TestContext.Current.CancellationToken);
        await AuditEventCounters.InsertOneAsync(
            new AuditEventCounter { Id = "audit_event", Sequence = 100 },
            cancellationToken: TestContext.Current.CancellationToken
        );
        var metrics = Substitute.For<IComplianceDeclarationMetrics>();
        var subject = CreateSubject(metrics: metrics);

        await subject.Apply(TestContext.Current.CancellationToken);
        await subject.Apply(TestContext.Current.CancellationToken);

        var expected = Corrected(original);
        (await Read(original)).Should().BeEquivalentTo(expected);
        (await Read(unchanged)).Should().BeEquivalentTo(unchanged);
        var events = await AuditEvents
            .Find(x => x.EntityId == original.Id.ToString())
            .ToListAsync(TestContext.Current.CancellationToken);
        events.Should().HaveCount(2);
        events.Single(x => x.EventId == historical.EventId).Should().BeEquivalentTo(historical);
        var auditEvent = events.Single(x => x.EventId != historical.EventId);
        auditEvent.Sequence.Should().Be(101);
        auditEvent.Actor.Should().Be(AuditActors.Service);
        auditEvent.Operation.Should().Be("update");
        auditEvent.EventType.Should().Be("submission.amended");
        auditEvent.Version.Should().Be(8);
        auditEvent.SchemaVersion.Should().Be(ComplianceDeclaration.SchemaVersionValue);
        auditEvent.OccurredAt.Should().Be(s_correctedAt.UtcDateTime);
        auditEvent.Before.Should().Equal(original.ToBsonDocument());
        auditEvent.After.Should().Equal(expected.ToBsonDocument());
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
        (
            await AuditEvents.CountDocumentsAsync(
                x => x.EntityId == unchanged.Id.ToString(),
                cancellationToken: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be(0);
        metrics.Received(1).Updated(status);
    }

    [Theory]
    [InlineData("v1.2")]
    [InlineData("v1.4")]
    public async Task Apply_ShouldExcludeOtherSchemasBeforeDeserialisingEntities(string schemaVersion)
    {
        // Raw BSON represents a different schema that the current entity must not deserialize.
        var document = Declaration().ToBsonDocument();
        document["schemaVersion"] = schemaVersion;
        document["obligations"] = "a different schema";
        var collection = GetMongoDatabase().GetCollection<BsonDocument>(nameof(ComplianceDeclaration));
        await collection.InsertOneAsync(document, cancellationToken: TestContext.Current.CancellationToken);

        await CreateSubject().Apply(TestContext.Current.CancellationToken);

        (
            await collection
                .Find(Builders<BsonDocument>.Filter.Eq("_id", document["_id"]))
                .SingleAsync(TestContext.Current.CancellationToken)
        )
            .Should()
            .Equal(document);
    }

    [Fact]
    public async Task Run_WhenCorrectionFails_ShouldRetryWithoutDuplicateUpdatesAndCompleteOnlyAfterSuccess()
    {
        var original = Declaration();
        var malformed = Declaration() with { Obligations = null! };
        await ComplianceDeclarations.InsertManyAsync(
            [original, malformed],
            cancellationToken: TestContext.Current.CancellationToken
        );
        var correction = CreateSubject();
        var history = GetMongoDatabase().GetCollection<ApplicationDataCorrectionCompletion>(CheckpointsCollectionName);
        await history.DeleteOneAsync(x => x.Id == correction.Id, TestContext.Current.CancellationToken);
        var runner = new ApplicationDataCorrectionRunner(
            GetMongoApplicationDatabase(),
            [correction],
            new FakeTimeProvider(s_correctedAt),
            Substitute.For<ILogger<ApplicationDataCorrectionRunner>>()
        );

        var action = () => runner.Run(TestContext.Current.CancellationToken);
        (await action.Should().ThrowAsync<InvalidOperationException>())
            .Which.InnerException.Should()
            .BeOfType<ArgumentNullException>();
        (await Read(original)).Should().BeEquivalentTo(Corrected(original));
        (await Read(malformed)).Should().BeEquivalentTo(malformed);
        (await history.Find(x => x.Id == correction.Id).AnyAsync(TestContext.Current.CancellationToken))
            .Should()
            .BeFalse();
        var repaired = malformed with { Obligations = original.Obligations };
        await ComplianceDeclarations.ReplaceOneAsync(
            x => x.Id == repaired.Id,
            repaired,
            cancellationToken: TestContext.Current.CancellationToken
        );

        await runner.Run(TestContext.Current.CancellationToken);
        await runner.Run(TestContext.Current.CancellationToken);

        (await Read(original)).Should().BeEquivalentTo(Corrected(original));
        (await Read(repaired)).Should().BeEquivalentTo(Corrected(repaired));
        var events = await AuditEvents
            .Find(x => x.EventType == "submission.amended")
            .ToListAsync(TestContext.Current.CancellationToken);
        events.Should().HaveCount(2);
        events.Select(x => x.EntityId).Should().OnlyHaveUniqueItems();
        (await history.Find(x => x.Id == correction.Id).SingleAsync(TestContext.Current.CancellationToken))
            .CompletedAt.Should()
            .Be(s_correctedAt.UtcDateTime);
    }

    [Fact]
    public async Task Apply_WhenAuditFails_ShouldRollBackTheServiceUpdateAndAllowRetry()
    {
        var original = Declaration();
        await ComplianceDeclarations.InsertOneAsync(original, cancellationToken: TestContext.Current.CancellationToken);
        var failing = Substitute.For<IAuditEventService>();
        failing
            .RecordEvent(Arg.Any<IClientSessionHandle>(), Arg.Any<AuditEventRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Audit storage unavailable")));
        var action = () => CreateSubject(failing).Apply(TestContext.Current.CancellationToken);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .Which.InnerException.Should()
            .BeOfType<InvalidOperationException>();
        (await Read(original)).Should().BeEquivalentTo(original);
        (
            await AuditEvents.CountDocumentsAsync(
                x => x.EntityId == original.Id.ToString(),
                cancellationToken: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be(0);

        await CreateSubject().Apply(TestContext.Current.CancellationToken);

        (await Read(original)).Should().BeEquivalentTo(Corrected(original));
        (
            await AuditEvents.CountDocumentsAsync(
                x => x.EntityId == original.Id.ToString(),
                cancellationToken: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be(1);
    }

    [Fact]
    public async Task Run_ShouldUseExplicitVersionsAndSkipCompletedCorrections()
    {
        var database = GetMongoApplicationDatabase();
        var history = GetMongoDatabase().GetCollection<ApplicationDataCorrectionCompletion>(CheckpointsCollectionName);
        const string firstId = "ordering-first";
        const string secondId = "ordering-second";
        await history.DeleteManyAsync(
            Builders<ApplicationDataCorrectionCompletion>.Filter.In(x => x.Id, new[] { firstId, secondId }),
            TestContext.Current.CancellationToken
        );
        var order = new List<int>();
        var first = Substitute.For<IApplicationDataCorrection>();
        first.Version.Returns(1);
        first.Id.Returns(firstId);
        first
            .Apply(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                order.Add(1);
                return Task.CompletedTask;
            });
        var second = Substitute.For<IApplicationDataCorrection>();
        second.Version.Returns(2);
        second.Id.Returns(secondId);
        second
            .Apply(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                order.Add(2);
                return Task.CompletedTask;
            });
        var runner = new ApplicationDataCorrectionRunner(
            database,
            [second, first],
            new FakeTimeProvider(s_correctedAt),
            Substitute.For<ILogger<ApplicationDataCorrectionRunner>>()
        );

        await runner.Run(TestContext.Current.CancellationToken);
        await runner.Run(TestContext.Current.CancellationToken);

        order.Should().Equal(1, 2);
    }

    [Fact]
    public async Task Lease_ShouldExcludeOtherOwnersAndRemainIndependentOfSchemaMigrations()
    {
        await GetMongoDatabase()
            .DropCollectionAsync("_application_data_correction_lease", TestContext.Current.CancellationToken);
        var first = new ApplicationDataCorrectionLeaseService(GetMongoApplicationDatabase(), TimeProvider.System);
        var second = new ApplicationDataCorrectionLeaseService(GetMongoApplicationDatabase(), TimeProvider.System);
        var migration = new MongoMigrationLeaseService(GetMongoDatabase(), TimeProvider.System);
        await GetMongoDatabase().DropCollectionAsync("_migrations_lease", TestContext.Current.CancellationToken);
        var duration = TimeSpan.FromMinutes(1);

        (await first.TryAcquire(duration, TestContext.Current.CancellationToken)).Should().BeTrue();
        (await second.TryAcquire(duration, TestContext.Current.CancellationToken)).Should().BeFalse();
        (await second.TryRenew(duration, TestContext.Current.CancellationToken)).Should().BeFalse();
        await second.Release(TestContext.Current.CancellationToken);
        (await first.TryRenew(duration, TestContext.Current.CancellationToken)).Should().BeTrue();
        (await migration.TryAcquire(duration, TestContext.Current.CancellationToken)).Should().BeTrue();
        await first.Release(TestContext.Current.CancellationToken);
        (await second.TryAcquire(duration, TestContext.Current.CancellationToken)).Should().BeTrue();
        await second.Release(TestContext.Current.CancellationToken);
        await migration.Release(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Apply_WhenConcurrentUpdateWins_ShouldPreserveItAndAuditFreshStateOnRetry()
    {
        var original = Declaration();
        await ComplianceDeclarations.InsertOneAsync(original, cancellationToken: TestContext.Current.CancellationToken);
        var database = GetMongoApplicationDatabase();
        var actual = database.GetCollection<ComplianceDeclaration>(nameof(ComplianceDeclaration));
        var intercepted = Substitute.For<IMongoCollection<ComplianceDeclaration>>();
        intercepted.DocumentSerializer.Returns(actual.DocumentSerializer);
        intercepted.Settings.Returns(actual.Settings);
        intercepted
            .FindAsync(
                Arg.Any<FilterDefinition<ComplianceDeclaration>>(),
                Arg.Any<FindOptions<ComplianceDeclaration, ComplianceDeclaration>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(x =>
                actual.FindAsync(
                    x.Arg<FilterDefinition<ComplianceDeclaration>>(),
                    x.Arg<FindOptions<ComplianceDeclaration, ComplianceDeclaration>>(),
                    x.Arg<CancellationToken>()
                )
            );
        var concurrent = original with
        {
            Version = 8,
            Obligations = [Obligation(0, 90, "NotMet"), Obligation(0, 10, "NotMet")],
        };
        var firstWrite = true;
        intercepted
            .ReplaceOneAsync(
                Arg.Any<IClientSessionHandle>(),
                Arg.Any<FilterDefinition<ComplianceDeclaration>>(),
                Arg.Any<ComplianceDeclaration>(),
                Arg.Any<ReplaceOptions>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(async x =>
            {
                if (firstWrite)
                {
                    firstWrite = false;
                    await ComplianceDeclarations.ReplaceOneAsync(
                        x => x.Id == original.Id,
                        concurrent,
                        cancellationToken: TestContext.Current.CancellationToken
                    );
                }

                return await actual.ReplaceOneAsync(
                    x.Arg<IClientSessionHandle>(),
                    x.Arg<FilterDefinition<ComplianceDeclaration>>(),
                    x.Arg<ComplianceDeclaration>(),
                    x.Arg<ReplaceOptions>(),
                    x.Arg<CancellationToken>()
                );
            });
        var realContext = new MongoDbContext(
            database,
            Options.Create(new MongoDbOptions()),
            Substitute.For<ILogger<MongoDbContext>>()
        );
        var context = Substitute.For<IDbContext>();
        context.ComplianceDeclarations.Returns(intercepted);
        context
            .ExecuteTransaction(
                Arg.Any<Func<IClientSessionHandle, CancellationToken, Task<ComplianceDeclaration>>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(x =>
                realContext.ExecuteTransaction(
                    x.Arg<Func<IClientSessionHandle, CancellationToken, Task<ComplianceDeclaration>>>(),
                    x.Arg<string>(),
                    x.Arg<CancellationToken>()
                )
            );
        var subject = CreateSubject(contextOverride: context);

        var action = () => subject.Apply(TestContext.Current.CancellationToken);
        (await action.Should().ThrowAsync<InvalidOperationException>())
            .Which.InnerException.Should()
            .BeOfType<ConcurrencyException>();
        (await Read(original)).Should().BeEquivalentTo(concurrent);
        (
            await AuditEvents.CountDocumentsAsync(
                x => x.EntityId == original.Id.ToString(),
                cancellationToken: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be(0);

        await subject.Apply(TestContext.Current.CancellationToken);

        var expected = Corrected(concurrent) with { Version = 9, ObligationCoveragePercentage = 0m };
        (await Read(original)).Should().BeEquivalentTo(expected);
        var auditEvent = await AuditEvents
            .Find(x => x.EntityId == original.Id.ToString())
            .SingleAsync(TestContext.Current.CancellationToken);
        auditEvent.Before.Should().Equal(concurrent.ToBsonDocument());
        auditEvent.After.Should().Equal(expected.ToBsonDocument());
    }

    private static ComplianceDeclarationCoverageCorrection CreateSubject(
        IAuditEventService? auditEventService = null,
        IDbContext? contextOverride = null,
        IComplianceDeclarationMetrics? metrics = null
    )
    {
        var database = GetMongoApplicationDatabase();
        var timeProvider = new FakeTimeProvider(s_correctedAt);
        var context = new MongoDbContext(
            database,
            Options.Create(new MongoDbOptions()),
            Substitute.For<ILogger<MongoDbContext>>()
        );
        var service = new ComplianceDeclarationService(
            contextOverride ?? context,
            Substitute.For<ILogger<ComplianceDeclarationService>>(),
            timeProvider,
            auditEventService
                ?? new AuditEventService(new AuditEventDbContext(database), timeProvider, new UlidEventIdGenerator()),
            metrics ?? Substitute.For<IComplianceDeclarationMetrics>(),
            new TraceIdReader(
                new HeaderPropagationValues(),
                Options.Create(new TraceHeader { Name = "x-cdp-request-id" })
            ),
            new UnsubmittedEligibilityVisibilityService(context)
        );

        return new ComplianceDeclarationCoverageCorrection(
            contextOverride ?? context,
            service,
            Options.Create(new ApplicationDataCorrectionOptions { BatchSize = 1 })
        );
    }

    private static ComplianceDeclaration Corrected(ComplianceDeclaration original) =>
        original with
        {
            ObligationCoveragePercentage = 50m,
            Version = 8,
            Updated = s_correctedAt.UtcDateTime,
        };

    private Task<ComplianceDeclaration> Read(ComplianceDeclaration original) =>
        ComplianceDeclarations.Find(x => x.Id == original.Id).SingleAsync(TestContext.Current.CancellationToken);

    private static ComplianceDeclaration Declaration() =>
        ComplianceDeclarationFixture
            .DirectProducer()
            .With(x => x.Version, 7)
            .With(x => x.Created, new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            .With(x => x.Updated, new DateTime(2023, 2, 1, 0, 0, 0, DateTimeKind.Utc))
            .With(x => x.ObligationCoveragePercentage, 90m)
            .With(x => x.Obligations, [Obligation(90, 90, "Met"), Obligation(0, 10, "NotMet")])
            .Create();

    private static Obligation Obligation(int accepted, int obligated, string status) =>
        ObligationFixture
            .Default()
            .With(x => x.Status, status)
            .With(
                x => x.Tonnages,
                ObligationTonnagesFixture
                    .Default()
                    .With(x => x.Accepted, accepted)
                    .With(x => x.Obligated, obligated)
                    .Create()
            )
            .Create();
}
