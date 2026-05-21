using Defra.WasteObligations.Testing.Authentication;
using Defra.WasteObligations.Testing.Extensions.WireMock;

namespace Defra.WasteObligations.Api.IntegrationTests.Scenarios;

public class HealthTests : IntegrationTestBase
{
    [Fact]
    public async Task WhenOrganisationFound_WithObligations_ResponseShouldBeOk()
    {
        const string prnCommonBackendAccessToken = nameof(prnCommonBackendAccessToken);
        const string accountBackendAccessToken = nameof(accountBackendAccessToken);
        await WireMockContext.WireMockAdminApi.StubTokenRequest(
            expiryInSeconds: 60,
            clientId: ClientIds.PrnCommonBackend,
            accessToken: prnCommonBackendAccessToken
        );
        await WireMockContext.WireMockAdminApi.StubTokenRequest(
            expiryInSeconds: 60,
            clientId: ClientIds.AccountBackend,
            accessToken: accountBackendAccessToken
        );
        await WireMockContext.WireMockAdminApi.StubPrnCommonBackendAdminHealth(prnCommonBackendAccessToken);
        await WireMockContext.WireMockAdminApi.StubAccountBackendAdminHealth(accountBackendAccessToken);
        await WireMockContext.WireMockAdminApi.StubWasteOrganisationsHealth(
            BasicAuthCredential.ForClient(ClientIds.WasteOrganisations)
        );
        // see appsettings.json for template IDs
        await WireMockContext.WireMockAdminApi.StubGovukNotifyTemplateRequest("5f64e3bd-d454-4a45-a9c6-9409bf940d7a");
        await WireMockContext.WireMockAdminApi.StubGovukNotifyTemplateRequest("b3223b0b-a467-40c1-9150-f78b76d11fd8");

        var client = CreateClient();

        var response = await client.GetAsync(Testing.Endpoints.Health.All(), TestContext.Current.CancellationToken);

        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .DontScrubGuids();
    }
}
