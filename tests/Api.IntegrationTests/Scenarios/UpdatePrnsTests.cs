using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Testing;
using Defra.WasteObligations.Testing.Authentication;
using Defra.WasteObligations.Testing.Extensions.WireMock;
using Defra.WasteObligations.Testing.Fixtures.Dtos;

namespace Defra.WasteObligations.Api.IntegrationTests.Scenarios;

public class UpdatePrnsTests : IntegrationTestBase
{
    [Fact]
    public async Task WhenPrnStatusesUpdated_ShouldReturnOkAfterOneBackendRequest()
    {
        await WireMockContext.WireMockAdminApi.StubTokenRequest(
            expiryInSeconds: 60,
            clientId: ClientIds.PrnCommonBackend
        );
        var organisationId = Guid.NewGuid();
        var user = UserFixture.Regulator().Create();
        await WireMockContext.WireMockAdminApi.StubWasteOrganisationsOrganisationRequest(
            organisationId,
            BasicAuthCredential.ForClient(ClientIds.WasteOrganisations)
        );
        await WireMockContext.WireMockAdminApi.StubPrnCommonBackendPrnStatusUpdateRequest(
            organisationId,
            Guid.Parse(user.Id),
            accessToken: OAuth2Extensions.AccessToken
        );

        Guid[] prnIds = [Guid.NewGuid(), Guid.NewGuid()];
        var client = CreateClient();

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.Prns.UpdateMany(organisationId),
            new UpdatePrnsRequest
            {
                Status = UpdatePrnStatus.Accepted,
                User = user,
                PrnIds = [.. prnIds.Select(x => x.ToString("D"))],
                ObligationYear = 2026,
            },
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var requests = await WireMockContext.WireMockAdminApi.GetPrnCommonBackendPrnStatusUpdates();

        requests.Should().ContainSingle();
        var body = requests[0].Request?.Body;
        body.Should().NotBeNull();
        var sent = JsonSerializer.Deserialize<PrnStatusUpdate[]>(body);
        sent.Should()
            .BeEquivalentTo(
                prnIds.Select(x => new PrnStatusUpdate
                {
                    PrnId = x,
                    Status = PrnCommonBackendStatus.Accepted,
                    ObligationYear = "2026",
                    ExpectedStatus = PrnCommonBackendStatus.AwaitingAcceptance,
                })
            );
    }
}
