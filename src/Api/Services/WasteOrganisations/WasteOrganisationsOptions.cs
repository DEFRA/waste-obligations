using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Defra.WasteObligations.Api.Services.WasteOrganisations;

public class WasteOrganisationsOptions
{
    public const string SectionName = "WasteOrganisations";

    [Required]
    public required string BaseAddress { get; init; }

    [Required]
    public required string ClientId { get; init; }

    [Required]
    public required string ClientSecret { get; init; }

    public string BasicAuthCredential => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}"));

    public void Configure(HttpClient httpClient)
    {
        httpClient.BaseAddress = new Uri(BaseAddress);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", BasicAuthCredential);

        if (httpClient.BaseAddress.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            httpClient.DefaultRequestVersion = HttpVersion.Version20;
    }
}
