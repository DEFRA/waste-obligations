using System.Net.Http.Headers;

namespace Defra.WasteObligations.Api.Utils.OAuth2;

public class OAuth2Handler(OAuth2TokenCache tokenCache) : DelegatingHandler
{
    public static HttpRequestOptionsKey<Action<TimeSpan>> TokenDurationCallbackKey { get; } =
        new(nameof(TokenDurationCallbackKey));

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var startingTimestamp = TimeProvider.System.GetTimestamp();
        var token = await tokenCache.GetToken(cancellationToken);

        if (request.Options.TryGetValue(TokenDurationCallbackKey, out var recordTokenDuration))
        {
            recordTokenDuration(TimeProvider.System.GetElapsedTime(startingTimestamp));
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
