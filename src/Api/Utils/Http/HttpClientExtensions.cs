namespace Defra.WasteObligations.Api.Utils.Http;

public static class HttpClientExtensions
{
    public static HttpRequestMessage CreateRequest(this HttpClient httpClient, HttpMethod method, string requestUri) =>
        new(method, requestUri)
        {
            Version = httpClient.DefaultRequestVersion,
            VersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
        };

    public static void ConfigureForResiliencePipeline(this HttpClient httpClient, bool addResiliencePipeline)
    {
        if (addResiliencePipeline)
        {
            httpClient.Timeout = Timeout.InfiniteTimeSpan;
        }
    }
}
