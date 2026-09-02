using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Services.AccountBackend;
using Defra.WasteObligations.Testing.Fixtures.AccountBackend;
using WireMock.Client;
using WireMock.Client.Extensions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Defra.WasteObligations.Testing.Extensions.WireMock;

public static class AccountBackendExtensions
{
    public static async Task StubAccountBackendAdminHealth(this IWireMockAdminApi wireMock, string? accessToken = null)
    {
        var builder = wireMock.GetMappingBuilder();

        builder.Given(x =>
            x.WithRequest(r =>
                {
                    r.UsingGet().WithPath("/admin/health");

                    if (accessToken is not null)
                        r.WithHeader("Authorization", $"Bearer {accessToken}");
                })
                .WithResponse(r => r.WithStatusCode(HttpStatusCode.OK))
        );

        var status = await builder.BuildAndPostAsync(TestContext.Current.CancellationToken);
        status.Guid.Should().NotBeNull();
    }

    public static async Task StubAccountBackendOrganisationWithPersonsRequest(
        this IWireMockAdminApi wireMock,
        Guid organisationId,
        string? accessToken = null,
        OrganisationWithPersons? organisationWithPersons = null
    )
    {
        var builder = wireMock.GetMappingBuilder();

        builder.Given(x =>
            x.WithRequest(r =>
                {
                    r.UsingGet().WithPath($"/api/organisations/organisation-with-persons/{organisationId:D}");

                    if (accessToken is not null)
                        r.WithHeader("Authorization", $"Bearer {accessToken}");
                })
                .WithResponse(r =>
                    r.WithStatusCode(HttpStatusCode.OK)
                        .WithBodyAsJson(
                            organisationWithPersons ?? OrganisationWithPersonsFixture.CancellationRecipients()
                        )
                )
        );

        var status = await builder.BuildAndPostAsync(TestContext.Current.CancellationToken);
        status.Guid.Should().NotBeNull();
    }

    public static void StubAccountBackendOrganisationWithPersonsRequest(
        this WireMockServer wireMock,
        Guid organisationId,
        string? accessToken = null,
        OrganisationWithPersons? organisationWithPersons = null
    )
    {
        var request = Request
            .Create()
            .UsingGet()
            .WithPath($"/api/organisations/organisation-with-persons/{organisationId:D}");

        if (accessToken is not null)
            request = request.WithHeader("Authorization", $"Bearer {accessToken}");

        wireMock
            .Given(request)
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithBodyAsJson(organisationWithPersons ?? OrganisationWithPersonsFixture.CancellationRecipients())
            );
    }
}
