using Defra.WasteObligations.Api.Utils.Http;
using Defra.WasteObligations.Api.Utils.OAuth2;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace Defra.WasteObligations.Api.Services.PrnCommonBackend;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPrnCommonBackendService(
        this IServiceCollection services,
        bool addResiliencePipeline
    )
    {
        const string name = PrnCommonBackendOptions.SectionName;

        services.AddOptions<PrnCommonBackendOptions>().BindConfiguration(name).ValidateDataAnnotations();
        services.AddOptions<HttpStandardResilienceOptions>(name).BindConfiguration(name);

        services
            .AddHttpClient(nameof(OAuth2TokenCache))
            .AddHeaderPropagation()
            .ConfigurePrimaryHttpMessageHandler<ProxyHttpMessageHandler>();
        services.AddKeyedSingleton<OAuth2TokenCache>(
            name,
            (sp, _) =>
                new OAuth2TokenCache(
                    sp.GetRequiredService<IHttpClientFactory>(),
                    sp.GetRequiredService<IOptions<PrnCommonBackendOptions>>().Value
                )
        );
        services.AddKeyedTransient<OAuth2Handler>(
            name,
            (sp, _) => new OAuth2Handler(sp.GetRequiredKeyedService<OAuth2TokenCache>(name))
        );

        var httpClientBuilder = services
            .AddHttpClient<IPrnCommonBackendService, PrnCommonBackendService>()
            .AddHttpMessageHandler(sp => sp.GetRequiredKeyedService<OAuth2Handler>(name))
            .ConfigureHttpClient(
                (sp, httpClient) =>
                {
                    sp.GetRequiredService<IOptions<PrnCommonBackendOptions>>().Value.Configure(httpClient);

                    if (addResiliencePipeline)
                    {
                        // See resilience handler below for timeout control
                        httpClient.Timeout = Timeout.InfiniteTimeSpan;
                    }
                }
            )
            .AddHeaderPropagation();

        if (addResiliencePipeline)
        {
            httpClientBuilder.AddResilienceHandler(
                nameof(IPrnCommonBackendService),
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

        return services;
    }
}
