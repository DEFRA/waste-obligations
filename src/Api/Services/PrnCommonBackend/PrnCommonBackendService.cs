using System.Net;
using Defra.WasteObligations.Api.Utils.Http;

namespace Defra.WasteObligations.Api.Services.PrnCommonBackend;

public class PrnCommonBackendService(HttpClient httpClient) : IPrnCommonBackendService
{
    public async Task<Obligations?> ReadObligations(Guid organisationId, int year, CancellationToken cancellationToken)
    {
        var request = httpClient.CreateRequest(HttpMethod.Get, $"api/v1/prn/obligationcalculation/{year}");
        request.Headers.Add("X-EPR-ORGANISATION", organisationId.ToString("D"));

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Obligations?>(cancellationToken);
    }
}
