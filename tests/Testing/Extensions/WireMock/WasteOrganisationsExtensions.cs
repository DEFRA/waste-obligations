using System.Net;
using AutoFixture;
using Defra.WasteObligations.Testing.Fixtures.WasteOrganisations;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Defra.WasteObligations.Testing.Extensions.WireMock;

public static class WasteOrganisationsExtensions
{
    public static void StubWasteOrganisationsOrganisationRequest(
        this WireMockServer wireMock,
        string organisationId,
        string? basicAuthToken = null
    )
    {
        var request = Request.Create().UsingGet().WithPath($"/organisations/{organisationId}");

        if (basicAuthToken is not null)
            request = request.WithHeader("Authorization", $"Basic {basicAuthToken}");

        wireMock
            .Given(request)
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithBodyAsJson(OrganisationFixture.Default().Create())
            );
    }
}
