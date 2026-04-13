using System.Net;
using Defra.WasteObligations.Api.Utils.Http;

namespace Defra.WasteObligations.Api.Services.WasteOrganisations;

public class WasteOrganisationsService(HttpClient httpClient) : IWasteOrganisationsService
{
    public async Task<Organisation?> ReadOrganisation(Guid organisationId, CancellationToken cancellationToken)
    {
        var request = httpClient.CreateRequest(HttpMethod.Get, $"organisations/{organisationId:D}");

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Organisation?>(cancellationToken);
    }
}
