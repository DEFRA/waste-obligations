using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Defra.WasteObligations.Api.Utils.Http;

[ExcludeFromCodeCoverage]
// ReSharper disable once ClassNeverInstantiated.Global
public class ProxyHttpMessageHandler : HttpClientHandler
{
    public ProxyHttpMessageHandler(ILogger<ProxyHttpMessageHandler> logger)
    {
        var proxyUri = Environment.GetEnvironmentVariable("HTTP_PROXY");
        var proxy = new WebProxy { BypassProxyOnLocal = true };

        if (proxyUri != null)
        {
            var uri = new UriBuilder(proxyUri).Uri;
            proxy.Address = uri;
        }
        else
        {
            logger.LogWarning("HTTP_PROXY is NOT set, proxy client will be disabled");
        }

        Proxy = proxy;
        UseProxy = proxyUri != null;
    }
}
