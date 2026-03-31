using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Testing;

namespace Defra.WasteObligations.Api.Tests.Endpoints.Organisations.Obligations;

public class GetTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    [Fact]
    public async Task WhenFound_ShouldReturnObligations()
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);
        var id = new Guid("923fa611-571c-4948-ab7d-fbb75e75ed65");

        var response = await client.GetStringAsync(
            Testing.Endpoints.Organisations.Obligations.Get(id, EndpointQuery.New.Where(EndpointFilter.Year(2026))),
            TestContext.Current.CancellationToken
        );

        await VerifyJson(response).DontScrubGuids();
    }

    [Fact]
    public async Task WhenNotFound_ShouldBeNotFound()
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Obligations.Get(
                Guid.NewGuid(),
                EndpointQuery.New.Where(EndpointFilter.Year(2026))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
