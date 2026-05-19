using WireMock.Admin.Mappings;
using WireMock.Admin.Requests;
using WireMock.Client;

namespace Defra.WasteObligations.Testing.Extensions.WireMock;

public static class GovukNotificationExtensions
{
    public static async Task<IList<LogEntryModel>> GetGovukNotifySendEmail(this IWireMockAdminApi wireMock)
    {
        var requestsModel = new RequestModel { Methods = ["POST"], Path = "/v2/notifications/email" };

        return await wireMock.FindRequestsAsync(requestsModel);
    }
}
