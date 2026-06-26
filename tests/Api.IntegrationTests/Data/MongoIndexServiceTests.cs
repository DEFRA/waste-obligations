using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using NSubstitute;

namespace Defra.WasteObligations.Api.IntegrationTests.Data;

public class MongoIndexServiceTests : IntegrationTestBase
{
    [Fact]
    public async Task Start_ShouldCreateIndex()
    {
        var database = GetMongoDatabase();
        var subject = new MongoIndexService(database, Substitute.For<ILogger<MongoIndexService>>());

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
        auditEventIndexes.Should().Contain(x => IsIndex(x, "Sequence", sequenceKeys, unique: true));
        auditEventIndexes.Should().Contain(x => IsIndex(x, "Entity_EntityId_Version", entityKeys));
        auditEventIndexes.Should().Contain(x => IsIndex(x, "Dispatch_analytics", dispatchKeys));
    }

    private static bool IsIndex(BsonDocument index, string name, BsonDocument keys, bool unique = false) =>
        index.GetValue("name") == name
        && index.GetValue("key").AsBsonDocument == keys
        && (!unique || index.GetValue("unique", false).AsBoolean);
}
