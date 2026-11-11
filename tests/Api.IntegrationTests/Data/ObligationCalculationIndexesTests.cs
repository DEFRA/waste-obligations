using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Data.Migrations;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.IntegrationTests.Data;

public class ObligationCalculationIndexesTests : IntegrationTestBase
{
    [Fact]
    public async Task Migration_ShouldCreateUniqueIdentitiesAndSupportRepeatedUpAndDown()
    {
        var subject = new ObligationCalculationIndexes();
        var context = new MigrationContext(GetMongoDatabase(), null!, TestContext.Current.CancellationToken);
        var current = GetMongoDatabase().GetCollection<BsonDocument>(nameof(ObligationCalculation));
        var history = GetMongoDatabase().GetCollection<BsonDocument>(nameof(ObligationCalculationHistory));
        try
        {
            await subject.UpAsync(context);
            await subject.UpAsync(context);
            var indexes = await (await current.Indexes.ListAsync(TestContext.Current.CancellationToken)).ToListAsync(
                TestContext.Current.CancellationToken
            );
            var identity = indexes.Single(x => x["name"] == ObligationCalculationIndexes.IdentityIndexName);
            identity["key"].Should().Be(new BsonDocument { { "submitterOrganisationId", 1 }, { "obligationYear", 1 } });
            identity["unique"].AsBoolean.Should().BeTrue();
            indexes
                .Single(x => x["name"] == "ProducerOrganisationId")["key"]
                .Should()
                .Be(new BsonDocument("producers.producerOrganisationId", 1));
            var historicalIndexes = await (
                await history.Indexes.ListAsync(TestContext.Current.CancellationToken)
            ).ToListAsync(TestContext.Current.CancellationToken);
            var historicalIdentity = historicalIndexes.Single(x => x["name"] == "CalculationId_Version");
            historicalIdentity["key"]
                .Should()
                .Be(new BsonDocument { { "calculation._id", 1 }, { "calculation.version", 1 } });
            historicalIdentity["unique"].AsBoolean.Should().BeTrue();

            await subject.DownAsync(context);
            await subject.DownAsync(context);
            (
                await (await current.Indexes.ListAsync(TestContext.Current.CancellationToken)).ToListAsync(
                    TestContext.Current.CancellationToken
                )
            )
                .Should()
                .OnlyContain(x => x["name"] == "_id_");
            (
                await (await history.Indexes.ListAsync(TestContext.Current.CancellationToken)).ToListAsync(
                    TestContext.Current.CancellationToken
                )
            )
                .Should()
                .OnlyContain(x => x["name"] == "_id_");
        }
        finally
        {
            await subject.UpAsync(context);
        }
    }
}
