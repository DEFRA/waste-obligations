using System.Net;
using Defra.WasteObligations.Api.Utils.Http;

namespace Defra.WasteObligations.Api.Services.AccountBackend;

public class AccountBackendService(HttpClient httpClient) : IAccountBackendService
{
    public async Task<OrganisationWithPersons?> ReadOrganisationWithPersons(
        Guid organisationId,
        CancellationToken cancellationToken
    )
    {
        var request = httpClient.CreateRequest(
            HttpMethod.Get,
            $"api/organisations/organisation-with-persons/{organisationId:D}"
        );

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<OrganisationWithPersons>(cancellationToken);
    }
}
