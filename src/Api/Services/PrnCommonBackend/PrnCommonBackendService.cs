using System.Globalization;
using System.Net;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Utils.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace Defra.WasteObligations.Api.Services.PrnCommonBackend;

public class PrnCommonBackendService(HttpClient httpClient, ILogger<PrnCommonBackendService> logger)
    : IPrnCommonBackendService
{
    private const string OrganisationHeaderName = "X-EPR-ORGANISATION";
    private const string UserHeaderName = "X-EPR-USER";

    public async Task<IEnumerable<Obligation>> ReadObligations(
        Guid organisationId,
        int year,
        CancellationToken cancellationToken
    )
    {
        var request = CreateOrganisationRequest(
            HttpMethod.Get,
            $"api/v1/prn/obligationcalculation/{year}",
            organisationId
        );

        var response = await Send(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return [];

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.Equals(responseBody, $"Invalid year provided: {year}.", StringComparison.Ordinal))
            {
                logger.LogInformation(
                    "PRN common backend has no obligation data for organisation {OrganisationId} and unsupported obligation year {ObligationYear}",
                    organisationId,
                    year
                );

                return [];
            }
        }

        response.EnsureSuccessStatusCode();

        var obligations = await response.Content.ReadFromJsonAsync<Obligations?>(cancellationToken);

        return obligations?.ObligationData ?? [];
    }

    public async Task<PrnData?> ReadPrn(Guid organisationId, string prnId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(prnId, out var commonBackendPrnId))
            return null;

        var request = CreateOrganisationRequest(HttpMethod.Get, $"api/v1/prn/{commonBackendPrnId:D}", organisationId);

        var response = await Send(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PrnData>(cancellationToken);
    }

    public async Task<PrnSearchResponse> SearchPrns(
        Guid organisationId,
        PrnSearchRequest search,
        CancellationToken cancellationToken
    )
    {
        var path = QueryHelpers.AddQueryString(
            "api/v1/prn/search",
            new Dictionary<string, string?>
            {
                ["page"] = search.Page.ToString(CultureInfo.InvariantCulture),
                ["pageSize"] = search.PageSize.ToString(CultureInfo.InvariantCulture),
                ["search"] = search.Search,
                ["filterBy"] = search.FilterBy,
                ["sortBy"] = search.SortBy,
            }
        );
        var request = CreateOrganisationRequest(HttpMethod.Get, path, organisationId);

        var response = await Send(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PrnSearchResponse>(cancellationToken);

        return result ?? throw new InvalidOperationException("PRN common backend returned an empty search response");
    }

    private HttpRequestMessage CreateOrganisationRequest(HttpMethod method, string path, Guid organisationId)
    {
        var request = httpClient.CreateRequest(method, path);
        request.Headers.Add(OrganisationHeaderName, organisationId.ToString("D"));

        return request;
    }

    private async Task<HttpResponseMessage> Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var organisationId = request.Headers.GetValues(OrganisationHeaderName).Single();

        if (request.Headers.TryGetValues(UserHeaderName, out var userHeaderValues))
        {
            var userId = userHeaderValues.Single();
            logger.LogInformation(
                "Sending PRN common backend request with X-EPR-ORGANISATION header value {OrganisationId} and X-EPR-USER header value {UserId}",
                organisationId,
                userId
            );
        }
        else
        {
            logger.LogInformation(
                "Sending PRN common backend request with X-EPR-ORGANISATION header value {OrganisationId}",
                organisationId
            );
        }

        return await httpClient.SendAsync(request, cancellationToken);
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

        var request = CreateOrganisationRequest(HttpMethod.Post, "api/v1/prn/status", organisationId);
        request.Headers.Add(UserHeaderName, userId.ToString("D"));
        request.Content = JsonContent.Create(
            new[]
            {
                new PrnStatusUpdate { PrnId = commonBackendPrnId, Status = status },
            }
        );

        var response = await Send(request, cancellationToken);

        return response.StatusCode switch
        {
            HttpStatusCode.OK => PrnStatusUpdateResult.Updated,
            HttpStatusCode.NotFound => PrnStatusUpdateResult.NotFound,
            HttpStatusCode.Conflict => throw new ConcurrencyException("The PRN status has already been updated."),
            _ => EnsureSuccessAndReturnUpdated(response),
        };
    }

    private static PrnStatusUpdateResult EnsureSuccessAndReturnUpdated(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();

        return PrnStatusUpdateResult.Updated;
    }
}
