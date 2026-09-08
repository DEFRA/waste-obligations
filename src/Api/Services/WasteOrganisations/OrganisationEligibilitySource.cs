using System.Diagnostics;
using Defra.WasteObligations.Api.Utils.Http;
using Defra.WasteObligations.Api.Utils.Metrics;

namespace Defra.WasteObligations.Api.Services.WasteOrganisations;

public class OrganisationEligibilitySource(HttpClient httpClient, IOrganisationEligibilityRefreshMetrics metrics)
    : IOrganisationEligibilitySource
{
    public async Task<OrganisationSearch> Search(CancellationToken cancellationToken)
    {
        var sourceStopwatch = Stopwatch.StartNew();

        try
        {
            var request = httpClient.CreateRequest(HttpMethod.Get, "organisations");
            var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var source =
                await response.Content.ReadFromJsonAsync<OrganisationSearch>(cancellationToken)
                ?? throw new InvalidOperationException(
                    "Waste Organisations returned an empty organisation search response"
                );
            sourceStopwatch.Stop();
            metrics.WasteOrganisationsReadCompleted(source.Organisations.Length, sourceStopwatch.Elapsed);

            return source;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            sourceStopwatch.Stop();
            metrics.WasteOrganisationsReadFailed(sourceStopwatch.Elapsed);
            throw;
        }
    }
}
