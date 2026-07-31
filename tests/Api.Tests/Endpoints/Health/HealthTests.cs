using System.Net;
using AwesomeAssertions;

namespace Defra.WasteObligations.Api.Tests.Endpoints.Health;

public class HealthTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    [Fact]
    public async Task AuthorizedHealth_WhenUnauthenticated_ShouldBeUnauthorized()
    {
        var client = CreateClient(addAuthorizationHeader: false);

        var response = await client.GetAsync(
            Testing.Endpoints.Health.Authorized(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthorizedHealth_WhenAuthenticated_ShouldBeOk()
    {
        var client = CreateClient();

        var response = await client.GetAsync(
            Testing.Endpoints.Health.Authorized(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_ShouldBeOk()
    {
        var client = CreateClient();

        var response = await client.GetAsync(Testing.Endpoints.Health.Ready(), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
