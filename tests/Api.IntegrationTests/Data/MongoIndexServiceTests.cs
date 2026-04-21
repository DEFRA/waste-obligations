using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Microsoft.Extensions.Logging;
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

        var indexes = await (
            await ComplianceDeclarations.Indexes.ListAsync(TestContext.Current.CancellationToken)
        ).ToListAsync(TestContext.Current.CancellationToken);

        indexes.Should().Contain(x => x.GetValue("name") == "OrganisationId_ObligationYear");
    }
}
