using System.Net.Http.Headers;

namespace Defra.WasteObligations.Api.Utils.OAuth2;

public class OAuth2Handler(OAuth2TokenCache tokenCache) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var token = await tokenCache.GetToken(cancellationToken);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
