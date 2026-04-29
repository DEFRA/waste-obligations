using System.Net;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Testing.Fixtures.PrnCommonBackend;
using WireMock.Client;
using WireMock.Client.Extensions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Defra.WasteObligations.Testing.Extensions.WireMock;

public static class PrnCommonBackendExtensions
{
    public static void StubPrnCommonBackendObligationsRequest(
        this WireMockServer wireMock,
        int year = 2026,
        string? organisationId = null,
        string? accessToken = null
    )
    {
        var request = Request.Create().UsingGet().WithPath($"/api/v1/prn/obligationcalculation/{year}");

        if (organisationId is not null)
            request = request.WithHeader("X-EPR-ORGANISATION", organisationId);

        if (accessToken is not null)
            request = request.WithHeader("Authorization", $"Bearer {accessToken}");

        wireMock
            .Given(request)
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithBodyAsJson(ObligationsFixture.Default().Create())
            );
    }

    public static async Task StubPrnCommonBackendObligationsRequest(
        this IWireMockAdminApi wireMock,
        int year = 2026,
        string? organisationId = null,
        string? accessToken = null
    )
    {
        var builder = wireMock.GetMappingBuilder();

        builder.Given(x =>
            x.WithRequest(r =>
                {
                    r.UsingGet().WithPath($"/api/v1/prn/obligationcalculation/{year}");

                    if (organisationId is not null)
                        r.WithHeader("X-EPR-ORGANISATION", organisationId);

                    if (accessToken is not null)
                        r.WithHeader("Authorization", $"Bearer {accessToken}");
                })
                .WithResponse(r =>
                    r.WithStatusCode(HttpStatusCode.OK).WithBodyAsJson(ObligationsFixture.Default().Create())
                )
        );

        var status = await builder.BuildAndPostAsync(TestContext.Current.CancellationToken);
        status.Guid.Should().NotBeNull();
    }

    public static async Task StubPrnCommonBackendAdminHealth(
        this IWireMockAdminApi wireMock,
        string? accessToken = null
    )
    {
        var builder = wireMock.GetMappingBuilder();

        builder.Given(x =>
            x.WithRequest(r =>
                {
                    r.UsingGet().WithPath("/admin/health");

                    if (accessToken is not null)
                        r.WithHeader("Authorization", $"Bearer {accessToken}");
                })
                .WithResponse(r =>
                    r.WithStatusCode(HttpStatusCode.OK).WithBodyAsJson(ObligationsFixture.Default().Create())
                )
        );

        var status = await builder.BuildAndPostAsync(TestContext.Current.CancellationToken);
        status.Guid.Should().NotBeNull();
    }
}
