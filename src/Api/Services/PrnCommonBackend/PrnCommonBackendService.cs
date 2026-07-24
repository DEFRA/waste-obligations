using System.Net;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Utils.Http;

namespace Defra.WasteObligations.Api.Services.PrnCommonBackend;

public class PrnCommonBackendService(HttpClient httpClient) : IPrnCommonBackendService
{
    public async Task<IEnumerable<Obligation>> ReadObligations(
        Guid organisationId,
        int year,
        CancellationToken cancellationToken
    )
    {
        var request = httpClient.CreateRequest(HttpMethod.Get, $"api/v1/prn/obligationcalculation/{year}");
        request.Headers.Add("X-EPR-ORGANISATION", organisationId.ToString("D"));

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return [];

        response.EnsureSuccessStatusCode();

        var obligations = await response.Content.ReadFromJsonAsync<Obligations?>(cancellationToken);

        return obligations is not null ? obligations.ObligationData : [];
    }

    public async Task<PrnDetails?> ReadPrn(Guid organisationId, string prnId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(prnId, out var commonBackendPrnId))
            return null;

        var request = httpClient.CreateRequest(HttpMethod.Get, $"api/v1/prn/{commonBackendPrnId:D}");
        request.Headers.Add("X-EPR-ORGANISATION", organisationId.ToString("D"));

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PrnDetails>(cancellationToken);
    }

    public async Task<PrnStatusUpdateResult> UpdatePrnStatus(
        Guid organisationId,
        Guid userId,
        string prnId,
        string status,
        CancellationToken cancellationToken
    )
    {
        if (!Guid.TryParse(prnId, out var commonBackendPrnId))
            return PrnStatusUpdateResult.NotFound;

        var request = httpClient.CreateRequest(HttpMethod.Post, "api/v1/prn/status");
        request.Headers.Add("X-EPR-ORGANISATION", organisationId.ToString("D"));
        request.Headers.Add("X-EPR-USER", userId.ToString("D"));
        request.Content = JsonContent.Create(
            new[]
            {
                new PrnStatusUpdate { PrnId = commonBackendPrnId, Status = status },
            }
        );

        var response = await httpClient.SendAsync(request, cancellationToken);

        return response.StatusCode switch
        {
            HttpStatusCode.OK => PrnStatusUpdateResult.Updated,
            HttpStatusCode.NotFound => PrnStatusUpdateResult.NotFound,
            HttpStatusCode.Conflict => throw new ConcurrencyException("The PRN status has already been updated."),
            _ => HandleUnexpectedResponse(response),
        };
    }

    private static PrnStatusUpdateResult HandleUnexpectedResponse(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();

        return PrnStatusUpdateResult.Updated;
    }
}
