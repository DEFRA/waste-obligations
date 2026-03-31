using System.Net;
using AwesomeAssertions;

namespace Defra.WasteObligations.Api.IntegrationTests.Scenarios;

public class OpenApiTests : MongoTestBase
{
    [Fact]
    public async Task OpenApi_ShouldBeOk()
    {
        var client = CreateClient();

        var response = await client.GetAsync(Testing.Endpoints.OpenApi.V1, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
