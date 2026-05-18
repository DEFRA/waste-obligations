using Defra.WasteObligations.Api.Utils.Http;
using Defra.WasteObligations.Api.Utils.OAuth2;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace Defra.WasteObligations.Api.Services.AccountBackend;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAccountBackendService(
        this IServiceCollection services,
        bool addResiliencePipeline = true
    )
    {
        const string name = AccountBackendOptions.SectionName;

        services.AddOAuth2Client<AccountBackendOptions>(name);
        services.AddOptions<HttpStandardResilienceOptions>(name).BindConfiguration(name);

        var httpClientBuilder = services
            .AddHttpClient<IAccountBackendService, AccountBackendService>()
            .AddHttpMessageHandler(sp => sp.GetRequiredKeyedService<OAuth2Handler>(name))
            .ConfigurePrimaryHttpMessageHandler<ProxyHttpMessageHandler>()
            .ConfigureHttpClient(
                (sp, httpClient) =>
                {
                    sp.GetRequiredService<IOptions<AccountBackendOptions>>().Value.Configure(httpClient);

                    if (addResiliencePipeline)
                    {
                        httpClient.Timeout = Timeout.InfiniteTimeSpan;
                    }
                }
            )
            .AddHeaderPropagation();

        if (addResiliencePipeline)
        {
            httpClientBuilder.AddResilienceHandler(
                nameof(IAccountBackendService),
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
