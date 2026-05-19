using Defra.WasteObligations.Api.Utils.Http;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Notify.Client;
using Notify.Interfaces;

namespace Defra.WasteObligations.Api.Services.GovukNotify;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGovukNotify(this IServiceCollection services, bool addResiliencePipeline = true)
    {
        const string name = GovukNotifyOptions.SectionName;

        services.AddSingleton<IValidateOptions<GovukNotifyOptions>, GovukNotifyOptionsValidator>();
        services.AddOptions<GovukNotifyOptions>().BindConfiguration(name).ValidateDataAnnotations();
        services.AddOptions<HttpStandardResilienceOptions>(name).BindConfiguration(name);

        services
            .AddHttpClient<IGovukNotifyService, GovukNotifyService>()
            .ConfigurePrimaryHttpMessageHandler<ProxyHttpMessageHandler>()
            .ConfigureHttpClient(
                (_, httpClient) =>
                {
                    httpClient.ConfigureForResiliencePipeline(addResiliencePipeline);
                }
            )
            .AddResiliencePipeline(addResiliencePipeline, name);

        services.AddSingleton<Func<HttpClient, GovukNotifyOptions, IAsyncNotificationClient>>(sp =>
            (httpClient, options) =>
            {
                if (sp.GetRequiredService<IWebHostEnvironment>().IsDevelopment() && options.BaseAddress is not null)
                    return new NotificationClient(options.BaseAddress, options.ApiKey);

                return new NotificationClient(new HttpClientWrapper(httpClient), options.ApiKey);
            }
        );

        return services;
    }
}
