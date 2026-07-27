using System.Net;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Testing.Fixtures.PrnCommonBackend;
using WireMock.Admin.Mappings;
using WireMock.Admin.Requests;
using WireMock.Client;
using WireMock.Client.Extensions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Defra.WasteObligations.Testing.Extensions.WireMock;

public static class PrnCommonBackendExtensions
{
    public static async Task<IList<LogEntryModel>> GetPrnCommonBackendPrnStatusUpdates(this IWireMockAdminApi wireMock)
    {
        var requestsModel = new RequestModel { Methods = ["POST"], Path = "/api/v1/prn/status" };

        return await wireMock.FindRequestsAsync(requestsModel);
    }

    public static void StubPrnCommonBackendPrnStatusUpdateRequest(
        this WireMockServer wireMock,
        PrnStatusUpdate statusUpdate,
        Guid organisationId,
        Guid userId,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string? accessToken = null
    )
    {
        var request = Request
            .Create()
            .UsingPost()
            .WithPath("/api/v1/prn/status")
            .WithHeader("X-EPR-ORGANISATION", organisationId.ToString("D"))
            .WithHeader("X-EPR-USER", userId.ToString("D"))
            .WithBody($"[{{\"prnId\":\"{statusUpdate.PrnId:D}\",\"status\":\"{statusUpdate.Status}\"}}]");

        if (accessToken is not null)
            request = request.WithHeader("Authorization", $"Bearer {accessToken}");

        wireMock.Given(request).RespondWith(Response.Create().WithStatusCode(statusCode));
    }

    public static async Task StubPrnCommonBackendPrnStatusUpdateRequest(
        this IWireMockAdminApi wireMock,
        Guid organisationId,
        Guid userId,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string? accessToken = null
    )
    {
        var builder = wireMock.GetMappingBuilder();

        builder.Given(x =>
            x.WithRequest(r =>
                {
                    r.UsingPost()
                        .WithPath("/api/v1/prn/status")
                        .WithHeader("X-EPR-ORGANISATION", organisationId.ToString("D"))
                        .WithHeader("X-EPR-USER", userId.ToString("D"));

                    if (accessToken is not null)
                        r.WithHeader("Authorization", $"Bearer {accessToken}");
                })
                .WithResponse(r => r.WithStatusCode(statusCode))
        );

        var status = await builder.BuildAndPostAsync(TestContext.Current.CancellationToken);
        status.Guid.Should().NotBeNull();
    }

    public static void StubPrnCommonBackendPrnRequest(
        this WireMockServer wireMock,
        Guid prnId,
        PrnDetails? prn = null,
        string? organisationId = null,
        string? accessToken = null
    )
    {
        var request = Request.Create().UsingGet().WithPath($"/api/v1/prn/{prnId:D}");

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
                    .WithBodyAsJson(prn ?? PrnDetailsFixture.Default().With(x => x.ExternalId, prnId).Create())
            );
    }

    public static async Task StubPrnCommonBackendPrnRequest(
        this IWireMockAdminApi wireMock,
        Guid prnId,
        PrnDetails prn,
        string? organisationId = null,
        string? accessToken = null
    )
    {
        var builder = wireMock.GetMappingBuilder();

        builder.Given(x =>
            x.WithRequest(r =>
                {
                    r.UsingGet().WithPath($"/api/v1/prn/{prnId:D}");

                    if (organisationId is not null)
                        r.WithHeader("X-EPR-ORGANISATION", organisationId);

                    if (accessToken is not null)
                        r.WithHeader("Authorization", $"Bearer {accessToken}");
                })
                .WithResponse(r => r.WithStatusCode(HttpStatusCode.OK).WithBodyAsJson(prn))
        );

        var status = await builder.BuildAndPostAsync(TestContext.Current.CancellationToken);
        status.Guid.Should().NotBeNull();
    }

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
                .WithResponse(r => r.WithStatusCode(HttpStatusCode.OK))
        );

        var status = await builder.BuildAndPostAsync(TestContext.Current.CancellationToken);
        status.Guid.Should().NotBeNull();
    }
}
