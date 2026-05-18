using System.Diagnostics.CodeAnalysis;

namespace Defra.WasteObligations.Api.Utils.OAuth2;

public class OAuth2TokenCache(OAuth2Client oauth2Client, OAuth2Options options)
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private string? _accessToken;
    private DateTime _expiresAt = DateTime.MinValue;

    [MemberNotNullWhen(true, nameof(_accessToken))]
    private bool IsTokenValid() => _accessToken is not null && DateTime.UtcNow < _expiresAt;

    public async Task<string> GetToken(CancellationToken cancellationToken)
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
        var tokenResponse = await oauth2Client.RequestTokenAsync(options, cancellationToken);

        _expiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60);
        _accessToken = tokenResponse.AccessToken;

        return _accessToken;
    }
}
