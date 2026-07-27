using System.Net;
using System.Net.Http.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Testing;
using Defra.WasteObligations.Testing.Authentication;
using Defra.WasteObligations.Testing.Extensions.WireMock;
using Defra.WasteObligations.Testing.Fixtures.Dtos;

namespace Defra.WasteObligations.Api.IntegrationTests.Scenarios;

public class UpdatePrnTests : IntegrationTestBase
{
    [Fact]
    public async Task WhenPrnStatusUpdated_ShouldReturnOk()
    {
        await WireMockContext.WireMockAdminApi.StubTokenRequest(
            expiryInSeconds: 60,
            clientId: ClientIds.PrnCommonBackend
        );
        var organisationId = Guid.NewGuid();
        var user = UserFixture.Regulator().Create();
        var prnId = Guid.NewGuid();
        await WireMockContext.WireMockAdminApi.StubWasteOrganisationsOrganisationRequest(
            organisationId,
            BasicAuthCredential.ForClient(ClientIds.WasteOrganisations)
        );
        await WireMockContext.WireMockAdminApi.StubPrnCommonBackendPrnStatusUpdateRequest(
            organisationId,
            Guid.Parse(user.Id),
            accessToken: OAuth2Extensions.AccessToken
        );

        var client = CreateClient();

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.Prns.Update(organisationId, prnId.ToString("D")),
            new UpdatePrnRequest { Status = UpdatePrnStatus.Accepted, User = user },
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var requests = await WireMockContext.WireMockAdminApi.GetPrnCommonBackendPrnStatusUpdates();

        requests.Should().ContainSingle();
    }
}
