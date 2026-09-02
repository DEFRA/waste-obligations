using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace Defra.WasteObligations.Api.Utils.Http;

public static class HttpClientBuilderExtensions
{
    public static IHttpClientBuilder AddResiliencePipeline(
        this IHttpClientBuilder httpClientBuilder,
        bool addResiliencePipeline,
        string name,
        bool retryUnsafeHttpMethods = false
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

                    if (retryUnsafeHttpMethods)
                    {
                        options.Retry.ShouldHandle = static args =>
                            ValueTask.FromResult(HttpClientResiliencePredicates.IsTransient(args.Outcome));
                    }

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
