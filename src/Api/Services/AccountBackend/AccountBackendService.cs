using System.Net;
using System.Net.Http.Json;
using Defra.WasteObligations.Api.Utils.Http;

namespace Defra.WasteObligations.Api.Services.AccountBackend;

public class AccountBackendService(HttpClient httpClient) : IAccountBackendService
{
    public async Task<OrganisationsByExternalIdsResponse> SearchOrganisationsByExternalIds(
        IReadOnlyCollection<Guid> externalIds,
        CancellationToken cancellationToken
    )
    {
        var request = httpClient.CreateRequest(HttpMethod.Post, "api/organisations/organisations-by-externalIds");
        request.Content = JsonContent.Create(new OrganisationsByExternalIdsRequest { ExternalIds = externalIds });

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<OrganisationsByExternalIdsResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Account backend returned an empty organisations response");
    }

    public async Task<IReadOnlyList<AccountOrganisation>> SearchOrganisationsByCompaniesHouseNumbers(
        IReadOnlyCollection<string> companiesHouseNumbers,
        CancellationToken cancellationToken
    )
    {
        var request = httpClient.CreateRequest(
            HttpMethod.Post,
            "api/organisations/organisations-by-companies-house-numbers"
        );
        request.Content = JsonContent.Create(
            new OrganisationsByCompaniesHouseNumbersRequest { CompaniesHouseNumbers = companiesHouseNumbers }
        );

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<AccountOrganisation>>(cancellationToken)
            ?? throw new InvalidOperationException("Account backend returned an empty organisations response");
    }

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
