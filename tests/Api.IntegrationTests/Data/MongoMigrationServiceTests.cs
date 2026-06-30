using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Data.Migrations;
using Defra.WasteObligations.AuditEvents.Data;
using Defra.WasteObligations.AuditEvents.Entities;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using NSubstitute;
using AuditEventIndexesMigration = Defra.WasteObligations.Api.Data.Migrations.AuditEventIndexes;

namespace Defra.WasteObligations.Api.IntegrationTests.Data;

public class MongoMigrationServiceTests : IntegrationTestBase
{
    private const string OrganisationIdObligationYearIndexName = "OrganisationId_ObligationYear";
    private const string SearchIndexName = "ObligationYear_Status_OrganisationRegistrationType";
    private const string OrganisationNameIndexName = "OrganisationName";
    private const string SequenceIndexName = "Sequence";
    private const string EntityEntityIdVersionIndexName = "Entity_EntityId_Version";
    private const string DispatchAnalyticsIndexName = "Dispatch_analytics";

    [Fact]
    public async Task Start_ShouldCreateIndex()
    {
        var database = GetMongoDatabase();
        var subject = new MongoMigrationService(
            database,
            TimeProvider.System,
            Substitute.For<ILogger<MongoMigrationService>>()
        );

        await subject.StartAsync(TestContext.Current.CancellationToken);

        var complianceDeclarationIndexes = await (
            await ComplianceDeclarations.Indexes.ListAsync(TestContext.Current.CancellationToken)
        ).ToListAsync(TestContext.Current.CancellationToken);
        var auditEventIndexes = await (
            await AuditEvents.Indexes.ListAsync(TestContext.Current.CancellationToken)
        ).ToListAsync(TestContext.Current.CancellationToken);
        var sequenceKeys = new BsonDocument("sequence", 1);
        var entityKeys = new BsonDocument
        {
            ["entity"] = 1,
            ["entityId"] = 1,
            ["version"] = 1,
        };
        var dispatchKeys = new BsonDocument { ["dispatches.analytics"] = 1, ["sequence"] = 1 };

        complianceDeclarationIndexes.Should().Contain(x => x.GetValue("name") == "OrganisationId_ObligationYear");
        auditEventIndexes.Should().Contain(x => IsIndex(x, SequenceIndexName, sequenceKeys, unique: true));
        auditEventIndexes.Should().Contain(x => IsIndex(x, EntityEntityIdVersionIndexName, entityKeys));
        auditEventIndexes.Should().Contain(x => IsIndex(x, DispatchAnalyticsIndexName, dispatchKeys));
    }

    [Fact]
    public void AuditEventDbContext_ShouldUseSupportCollectionNameForCounters()
    {
        AuditEventCounters
            .CollectionNamespace.CollectionName.Should()
            .Be(AuditEventDbContext.AuditEventCounterCollectionName);
    }

    [Fact]
    public async Task ComplianceDeclarationIndexes_ShouldCreateReplaceAndDropIndexes()
    {
        var database = GetMongoDatabase();
        var context = new MigrationContext(database, null!, TestContext.Current.CancellationToken);
        var subject = new ComplianceDeclarationIndexes();
        await subject.DownAsync(context);
        await ComplianceDeclarations.Indexes.CreateOneAsync(
            new CreateIndexModel<ComplianceDeclaration>(
                Builders<ComplianceDeclaration>.IndexKeys.Ascending(x => x.Created),
                new CreateIndexOptions { Name = OrganisationIdObligationYearIndexName }
            ),
            cancellationToken: TestContext.Current.CancellationToken
        );

        await subject.UpAsync(context);

        var indexes = await ListComplianceDeclarationIndexes();
        indexes.Should().Contain(x => x.GetValue("name") == OrganisationIdObligationYearIndexName);
        indexes.Should().Contain(x => x.GetValue("name") == SearchIndexName);
        indexes.Should().Contain(x => x.GetValue("name") == OrganisationNameIndexName);
        indexes
            .Single(x => x.GetValue("name") == OrganisationIdObligationYearIndexName)
            .GetValue("key")
            .AsBsonDocument.Equals(new BsonDocument("created", 1))
            .Should()
            .BeFalse();

        await subject.DownAsync(context);
        await subject.DownAsync(context);
        indexes = await ListComplianceDeclarationIndexes();

        indexes.Should().NotContain(x => x.GetValue("name") == OrganisationIdObligationYearIndexName);
        indexes.Should().NotContain(x => x.GetValue("name") == SearchIndexName);
        indexes.Should().NotContain(x => x.GetValue("name") == OrganisationNameIndexName);

        await subject.UpAsync(context);
    }

    [Fact]
    public async Task AuditEventIndexes_ShouldCreateReplaceAndDropIndexes()
    {
        var database = GetMongoDatabase();
        var context = new MigrationContext(database, null!, TestContext.Current.CancellationToken);
        var subject = new AuditEventIndexesMigration();
        await subject.DownAsync(context);
        await AuditEvents.Indexes.CreateOneAsync(
            new CreateIndexModel<AuditEvent>(
                Builders<AuditEvent>.IndexKeys.Ascending(x => x.Actor),
                new CreateIndexOptions { Name = SequenceIndexName }
            ),
            cancellationToken: TestContext.Current.CancellationToken
        );

        await subject.UpAsync(context);

        var indexes = await ListAuditEventIndexes();
        var sequenceKeys = new BsonDocument("sequence", 1);
        var entityKeys = new BsonDocument
        {
            ["entity"] = 1,
            ["entityId"] = 1,
            ["version"] = 1,
        };
        var dispatchKeys = new BsonDocument { ["dispatches.analytics"] = 1, ["sequence"] = 1 };
        indexes.Should().Contain(x => IsIndex(x, SequenceIndexName, sequenceKeys, unique: true));
        indexes.Should().Contain(x => IsIndex(x, EntityEntityIdVersionIndexName, entityKeys));
        indexes.Should().Contain(x => IsIndex(x, DispatchAnalyticsIndexName, dispatchKeys));

        await subject.DownAsync(context);
        await subject.DownAsync(context);
        indexes = await ListAuditEventIndexes();

        indexes.Should().NotContain(x => x.GetValue("name") == SequenceIndexName);
        indexes.Should().NotContain(x => x.GetValue("name") == EntityEntityIdVersionIndexName);
        indexes.Should().NotContain(x => x.GetValue("name") == DispatchAnalyticsIndexName);

        await subject.UpAsync(context);
    }

    private static bool IsIndex(BsonDocument index, string name, BsonDocument keys, bool unique = false) =>
        index.GetValue("name") == name
        && index.GetValue("key").AsBsonDocument == keys
        && (!unique || index.GetValue("unique", false).AsBoolean);

    private async Task<List<BsonDocument>> ListComplianceDeclarationIndexes() =>
        await (await ComplianceDeclarations.Indexes.ListAsync(TestContext.Current.CancellationToken)).ToListAsync(
            TestContext.Current.CancellationToken
        );

    private async Task<List<BsonDocument>> ListAuditEventIndexes() =>
        await (await AuditEvents.Indexes.ListAsync(TestContext.Current.CancellationToken)).ToListAsync(
            TestContext.Current.CancellationToken
        );
}
