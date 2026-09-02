using Defra.WasteObligations.Api.Utils.Http;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Utils.OAuth2;

public static class OAuth2ServiceCollectionExtensions
{
    public static IServiceCollection AddOAuth2Client<TOptions>(this IServiceCollection services, string name)
        where TOptions : OAuth2Options
    {
        services.AddOptions<TOptions>().BindConfiguration(name).ValidateDataAnnotations();

        if (!HasOAuth2HttpClientRegistration(services))
        {
            services
                .AddOptions<HttpStandardResilienceOptions>(OAuth2Client.HttpClientName)
                .BindConfiguration(OAuth2Client.HttpClientName);
            services
                .AddHttpClient(OAuth2Client.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler<ProxyHttpMessageHandler>()
                .ConfigureHttpClient(httpClient => httpClient.ConfigureForResiliencePipeline(true))
                // This named client only sends idempotent client-credentials token requests.
                .AddResiliencePipeline(true, OAuth2Client.HttpClientName, retryUnsafeHttpMethods: true);
            services.AddSingleton(new OAuth2HttpClientRegistration(OAuth2Client.HttpClientName));
        }

        services.AddKeyedSingleton<OAuth2TokenCache>(
            name,
            (sp, _) =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var options = sp.GetRequiredService<IOptions<TOptions>>().Value;

                return new OAuth2TokenCache(new OAuth2Client(httpClientFactory), options);
            }
        );

        services.AddKeyedTransient<OAuth2Handler>(
            name,
            (sp, _) => new OAuth2Handler(sp.GetRequiredKeyedService<OAuth2TokenCache>(name))
        );

        return services;
    }

    private static bool HasOAuth2HttpClientRegistration(IServiceCollection services) =>
        services.Any(x => x.ServiceType == typeof(OAuth2HttpClientRegistration));

    private sealed record OAuth2HttpClientRegistration(string Name);
}
