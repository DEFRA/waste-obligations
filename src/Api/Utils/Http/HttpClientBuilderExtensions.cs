using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace Defra.WasteObligations.Api.Utils.Http;

public static class HttpClientBuilderExtensions
{
    public static IHttpClientBuilder AddResiliencePipeline(
        this IHttpClientBuilder httpClientBuilder,
        bool addResiliencePipeline,
        string name
    )
    {
        if (addResiliencePipeline)
        {
            httpClientBuilder.AddResilienceHandler(
                name,
                (builder, context) =>
                {
                    var options = context
                        .ServiceProvider.GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>()
                        .Get(name);

                    builder
                        .AddTimeout(options.TotalRequestTimeout)
                        .AddRetry(options.Retry)
                        .AddTimeout(options.AttemptTimeout);
                }
            );
        }

        return httpClientBuilder;
    }
}
