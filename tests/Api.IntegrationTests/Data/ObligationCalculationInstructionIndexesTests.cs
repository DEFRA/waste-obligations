using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Migrations;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.IntegrationTests.Data;

public class ObligationCalculationInstructionIndexesTests : IntegrationTestBase
{
    [Fact]
    public async Task Migration_ShouldCreateActiveScopeUniqueIndexAndSupportRepeatedUpAndDown()
    {
        var subject = new ObligationCalculationInstructionIndexes();
        var context = new MigrationContext(GetMongoDatabase(), null!, TestContext.Current.CancellationToken);
        const string indexName = "Active_ObligationYear_SubmitterId";

        try
        {
            await subject.UpAsync(context);
            await subject.UpAsync(context);

            var indexes = await (
                await ObligationCalculationInstructions.Indexes.ListAsync(TestContext.Current.CancellationToken)
            ).ToListAsync(TestContext.Current.CancellationToken);
            var index = indexes.Single(x => x["name"] == indexName);
            index["key"].Should().Be(new BsonDocument { { "obligationYear", 1 }, { "submitterId", 1 } });
            index["unique"].AsBoolean.Should().BeTrue();
            index["partialFilterExpression"]
                .Should()
                .Be(new BsonDocument("status", new BsonDocument("$in", new BsonArray { "Pending", "Running" })));

            await subject.DownAsync(context);
            await subject.DownAsync(context);

            indexes = await (
                await ObligationCalculationInstructions.Indexes.ListAsync(TestContext.Current.CancellationToken)
            ).ToListAsync(TestContext.Current.CancellationToken);
            indexes.Should().NotContain(x => x["name"] == indexName);
        }
        finally
        {
            await subject.UpAsync(context);
        }
    }
}
