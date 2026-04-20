using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Testing;
using Defra.WasteObligations.Testing.Authentication;
using Defra.WasteObligations.Testing.Extensions.WireMock;

namespace Defra.WasteObligations.Api.IntegrationTests.Scenarios;

public class ReadObligationsTests : IntegrationTestBase
{
    [Fact]
    public async Task WhenOrganisationFound_WithNoObligations_ResponseShouldBeOk()
    {
        await WireMockContext.WireMockAdminApi.StubTokenRequest(expiryInSeconds: 60);
        var organisationId = Guid.NewGuid();
        await WireMockContext.WireMockAdminApi.StubWasteOrganisationsOrganisationRequest(
            organisationId,
            BasicAuthCredential.Default
        );

        var client = CreateClient();

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Obligations.Read(
                organisationId,
                EndpointQuery.New.Where(EndpointFilter.ObligationYear(2026))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WhenOrganisationFound_WithObligations_ResponseShouldBeOk()
    {
        await WireMockContext.WireMockAdminApi.StubTokenRequest(expiryInSeconds: 60);
        var organisationId = Guid.NewGuid();
        await WireMockContext.WireMockAdminApi.StubWasteOrganisationsOrganisationRequest(
            organisationId,
            BasicAuthCredential.Default
        );
        const int year = 2026;
        await WireMockContext.WireMockAdminApi.StubPrnCommonBackendObligationsRequest(
            year,
            organisationId.ToString("D"),
            OAuth2Extensions.AccessToken
        );

        var client = CreateClient();

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Obligations.Read(
                organisationId,
                EndpointQuery.New.Where(EndpointFilter.ObligationYear(year))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
