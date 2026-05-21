using System.Net;
using AwesomeAssertions;
using WireMock.Admin.Mappings;
using WireMock.Admin.Requests;
using WireMock.Client;
using WireMock.Client.Extensions;

namespace Defra.WasteObligations.Testing.Extensions.WireMock;

public static class GovukNotificationExtensions
{
    public static async Task<IList<LogEntryModel>> GetGovukNotifySendEmail(this IWireMockAdminApi wireMock)
    {
        var requestsModel = new RequestModel { Methods = ["POST"], Path = "/v2/notifications/email" };

        return await wireMock.FindRequestsAsync(requestsModel);
    }

    public static async Task StubGovukNotifyTemplateRequest(this IWireMockAdminApi wireMock, string templateId)
    {
        var builder = wireMock.GetMappingBuilder();

        builder.Given(x =>
            x.WithRequest(r =>
                {
                    r.UsingGet().WithPath($"/v2/template/{templateId}");
                })
                .WithResponse(r =>
                    r.WithStatusCode(HttpStatusCode.OK).WithBodyAsJson(new { id = templateId, version = 1 })
                )
        );

        var status = await builder.BuildAndPostAsync(TestContext.Current.CancellationToken);
        status.Guid.Should().NotBeNull();
    }
}
