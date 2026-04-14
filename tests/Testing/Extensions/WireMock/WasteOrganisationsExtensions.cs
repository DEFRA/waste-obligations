using System.Net;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Testing.Fixtures.WasteOrganisations;
using WireMock.Client;
using WireMock.Client.Extensions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Defra.WasteObligations.Testing.Extensions.WireMock;

public static class WasteOrganisationsExtensions
{
    public static void StubWasteOrganisationsOrganisationRequest(
        this WireMockServer wireMock,
        Guid organisationId,
        string? basicAuthToken = null
    )
    {
        var request = Request.Create().UsingGet().WithPath($"/organisations/{organisationId:D}");

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

    public static async Task StubWasteOrganisationsOrganisationRequest(
        this IWireMockAdminApi wireMock,
        Guid organisationId,
        string? basicAuthToken = null
    )
    {
        var builder = wireMock.GetMappingBuilder();

        builder.Given(x =>
            x.WithRequest(r =>
                {
                    r.UsingGet().WithPath($"/organisations/{organisationId:D}");

                    if (basicAuthToken is not null)
                        r.WithHeader("Authorization", $"Basic {basicAuthToken}");
                })
                .WithResponse(r =>
                    r.WithStatusCode(HttpStatusCode.OK).WithBodyAsJson(OrganisationFixture.Default().Create())
                )
        );

        var status = await builder.BuildAndPostAsync(TestContext.Current.CancellationToken);
        status.Guid.Should().NotBeNull();
    }
}
