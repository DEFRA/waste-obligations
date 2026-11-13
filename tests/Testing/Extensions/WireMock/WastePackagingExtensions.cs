using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Services.WastePackaging;
using WireMock.Client;
using WireMock.Client.Extensions;

namespace Defra.WasteObligations.Testing.Extensions.WireMock;

public static class WastePackagingExtensions
{
    public static async Task StubWastePackagingRequest(
        this IWireMockAdminApi wireMock,
        int packagingYear,
        PackagingResponse response,
        string? basicAuthToken = null,
        string? traceId = null
    )
    {
        var builder = wireMock.GetMappingBuilder();
        builder.Given(x =>
            x.WithRequest(r =>
                {
                    r.UsingGet().WithPath($"/packaging/{packagingYear}/producer-material-totals-by-submitter");

                    if (basicAuthToken is not null)
                        r.WithHeader("Authorization", $"Basic {basicAuthToken}");
                    if (traceId is not null)
                        r.WithHeader("x-cdp-request-id", traceId);
                })
                .WithResponse(r =>
                    r.WithStatusCode(HttpStatusCode.OK)
                        .WithHeaders(new Dictionary<string, object> { ["Content-Type"] = "application/json" })
                        .WithBody(JsonSerializer.Serialize(response, JsonSerializerOptions.Web))
                )
        );
        var status = await builder.BuildAndPostAsync(TestContext.Current.CancellationToken);
        status.Guid.Should().NotBeNull();
    }
}
