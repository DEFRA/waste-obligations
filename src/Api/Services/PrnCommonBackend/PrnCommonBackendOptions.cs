using System.ComponentModel.DataAnnotations;
using System.Net;
using Defra.WasteObligations.Api.Utils.OAuth2;

namespace Defra.WasteObligations.Api.Services.PrnCommonBackend;

public class PrnCommonBackendOptions : OAuth2Options
{
    public const string SectionName = "PrnCommonBackend";

    [Required]
    public required string BaseAddress { get; init; }

    public void Configure(HttpClient httpClient)
    {
        httpClient.BaseAddress = new Uri(BaseAddress);

        if (httpClient.BaseAddress.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            httpClient.DefaultRequestVersion = HttpVersion.Version20;
    }
}
