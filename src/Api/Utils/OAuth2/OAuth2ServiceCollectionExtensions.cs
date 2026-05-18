using Defra.WasteObligations.Api.Utils.Http;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Utils.OAuth2;

public static class OAuth2ServiceCollectionExtensions
{
    public static IServiceCollection AddOAuth2Client<TOptions>(this IServiceCollection services, string name)
        where TOptions : OAuth2Options
    {
        const string clientName = "OAuth2Client";

        services.AddOptions<TOptions>().BindConfiguration(name).ValidateDataAnnotations();
        services.AddHttpClient(clientName).ConfigurePrimaryHttpMessageHandler<ProxyHttpMessageHandler>();
        services.AddKeyedSingleton<OAuth2TokenCache>(
            name,
            (sp, _) =>
            {
                var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(clientName);
                var options = sp.GetRequiredService<IOptions<TOptions>>().Value;

                return new OAuth2TokenCache(new OAuth2Client(httpClient), options);
            }
        );

        services.AddKeyedTransient<OAuth2Handler>(
            name,
            (sp, _) => new OAuth2Handler(sp.GetRequiredKeyedService<OAuth2TokenCache>(name))
        );

        return services;
    }
}
