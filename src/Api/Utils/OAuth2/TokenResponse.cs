using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Utils.OAuth2;

public class TokenResponse
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }
}
