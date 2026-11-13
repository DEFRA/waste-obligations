using Defra.WasteObligations.Api.Utils.Http;

namespace Defra.WasteObligations.Api.Services.Packaging;

public class PackagingSource(HttpClient httpClient) : IPackagingSource
{
    public async Task<PackagingResponse> GetFullYear(int packagingYear, CancellationToken cancellationToken)
    {
        using var request = httpClient.CreateRequest(
            HttpMethod.Get,
            $"packaging/{packagingYear}/producer-material-totals-by-submitter"
        );
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var source = await response.Content.ReadFromJsonAsync<PackagingResponse>(cancellationToken);
        if (source?.Items is null)
            throw new InvalidOperationException("Packaging API returned an unreadable full-year response");

        return source;
    }
}
