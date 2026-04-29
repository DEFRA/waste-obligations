using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Testing.Extensions.WireMock;

namespace Defra.WasteObligations.Api.IntegrationTests.Scenarios;

public class HealthTests : IntegrationTestBase
{
    [Fact]
    public async Task WhenOrganisationFound_WithObligations_ResponseShouldBeOk()
    {
        await WireMockContext.WireMockAdminApi.StubTokenRequest(expiryInSeconds: 60);
        await WireMockContext.WireMockAdminApi.StubPrnCommonBackendAdminHealth(OAuth2Extensions.AccessToken);

        var client = CreateClient();

        var response = await client.GetAsync(Testing.Endpoints.Health.All(), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
