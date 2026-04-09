namespace Defra.WasteObligations.Api.Utils.Http;

public static class HttpClientExtensions
{
    public static HttpRequestMessage CreateRequest(this HttpClient httpClient, HttpMethod method, string requestUri) =>
        new(method, requestUri)
        {
            Version = httpClient.DefaultRequestVersion,
            VersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
        };
}
