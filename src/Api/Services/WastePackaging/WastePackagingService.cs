using Defra.WasteObligations.Api.Utils.Http;
using Defra.WasteObligations.Api.Utils.Logging;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Services.WastePackaging;

public class WastePackagingService(HttpClient httpClient, IOptions<TraceHeader> traceHeader) : IWastePackagingService
{
    public async Task<PackagingResponse> Read(
        int packagingYear,
        CancellationToken cancellationToken,
        string? traceId = null
    )
    {
        using var request = httpClient.CreateRequest(
            HttpMethod.Get,
            $"packaging/{packagingYear}/producer-material-totals-by-submitter"
        );
        if (!string.IsNullOrWhiteSpace(traceId))
            request.Headers.TryAddWithoutValidation(traceHeader.Value.Name, traceId);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var source = await response.Content.ReadFromJsonAsync<PackagingResponse>(cancellationToken);
        if (source?.Items is null)
            throw new InvalidOperationException("Packaging API returned an unreadable full-year response");

        return source;
    }
}
