using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;

namespace Defra.WasteObligations.Api.Utils.OAuth2;

public class OAuth2Handler(IHttpClientFactory httpClientFactory, OAuth2Options options) : DelegatingHandler
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private string? _accessToken;
    private DateTime _expiresAt = DateTime.MinValue;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var token = await GetToken(cancellationToken);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }

    [MemberNotNullWhen(true, nameof(_accessToken))]
    private bool IsTokenValid() => _accessToken is not null && DateTime.UtcNow < _expiresAt;

    private async Task<string> GetToken(CancellationToken cancellationToken)
    {
        if (IsTokenValid())
            return _accessToken;

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (IsTokenValid())
                return _accessToken;

            return await RefreshToken(cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<string> RefreshToken(CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(nameof(OAuth2Handler));
        var values = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret,
        };

        if (options.Scope is not null)
            values.Add("scope", options.Scope);

        var body = new FormUrlEncodedContent(values);
        var response = await client.PostAsync(options.TokenEndpoint, body, cancellationToken);

        response.EnsureSuccessStatusCode();

        var tokenResponse =
            await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Empty token response");

        _expiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60);
        _accessToken = tokenResponse.AccessToken;

        return _accessToken;
    }
}
