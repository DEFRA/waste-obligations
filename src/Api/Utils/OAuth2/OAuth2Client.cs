namespace Defra.WasteObligations.Api.Utils.OAuth2;

public class OAuth2Client(HttpClient httpClient)
{
    public async Task<TokenResponse> RequestTokenAsync(OAuth2Options options, CancellationToken ct)
    {
        var values = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret,
        };

        if (options.Scope is not null)
            values.Add("scope", options.Scope);

        var body = new FormUrlEncodedContent(values);
        var response = await httpClient.PostAsync(options.TokenEndpoint, body, ct);

        response.EnsureSuccessStatusCode();

        var tokenResponse =
            await response.Content.ReadFromJsonAsync<TokenResponse>(ct)
            ?? throw new InvalidOperationException("Empty token response");

        return tokenResponse;
    }
}
