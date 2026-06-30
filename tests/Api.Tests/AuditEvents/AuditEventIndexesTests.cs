using AwesomeAssertions;
using Defra.WasteObligations.AuditEvents;
using Defra.WasteObligations.AuditEvents.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Tests.AuditEvents;

public class AuditEventIndexesTests
{
    static AuditEventIndexesTests()
    {
        Api.Data.ServiceCollectionExtensions.RegisterConventions();
    }

    [Fact]
    public void All_ShouldReturnAuditEventIndexes()
    {
        var indexes = AuditEventIndexes.All().ToList();

        indexes.Should().HaveCount(4);
        indexes.Should().ContainSingle(x => x.Name == "Sequence" && x.Unique);
        indexes.Should().ContainSingle(x => x.Name == "Entity_EntityId_Version" && !x.Unique);
        indexes.Should().ContainSingle(x => x.Name == "Dispatch_analytics" && !x.Unique);
        indexes.Should().ContainSingle(x => x.Name == "Dispatch_analytics_Status_Date" && !x.Unique);

        Render(indexes.Single(x => x.Name == "Sequence").Keys)
            .Equals(new BsonDocument("sequence", 1))
            .Should()
            .BeTrue();
        Render(indexes.Single(x => x.Name == "Entity_EntityId_Version").Keys)
            .Equals(
                new BsonDocument
                {
                    ["entity"] = 1,
                    ["entityId"] = 1,
                    ["version"] = 1,
                }
            )
            .Should()
            .BeTrue();
        Render(indexes.Single(x => x.Name == "Dispatch_analytics").Keys)
            .Equals(new BsonDocument { ["dispatches.analytics"] = 1, ["sequence"] = 1 })
            .Should()
            .BeTrue();
        Render(indexes.Single(x => x.Name == "Dispatch_analytics_Status_Date").Keys)
            .Equals(new BsonDocument { ["dispatches.analytics.status"] = 1, ["dispatches.analytics.date"] = 1 })
            .Should()
            .BeTrue();
    }

    private static BsonDocument Render(IndexKeysDefinition<AuditEvent> keys) =>
        keys.Render(
            new RenderArgs<AuditEvent>(BsonSerializer.LookupSerializer<AuditEvent>(), BsonSerializer.SerializerRegistry)
        );
}
