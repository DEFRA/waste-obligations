using Defra.WasteObligations.Api.Utils.Http;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Notify.Client;
using Notify.Interfaces;
using Polly;

namespace Defra.WasteObligations.Api.Services.GovukNotify;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGovukNotify(this IServiceCollection services, bool addResiliencePipeline = true)
    {
        const string name = GovukNotifyOptions.SectionName;

        services.AddSingleton<IValidateOptions<GovukNotifyOptions>, GovukNotifyOptionsValidator>();
        services.AddOptions<GovukNotifyOptions>().BindConfiguration(name).ValidateDataAnnotations();
        services.AddOptions<HttpStandardResilienceOptions>(name).BindConfiguration(name);

        var httpClientBuilder = services
            .AddHttpClient<IGovukNotifyService, GovukNotifyService>()
            .ConfigurePrimaryHttpMessageHandler<ProxyHttpMessageHandler>()
            .ConfigureHttpClient(
                (_, httpClient) =>
                {
                    if (addResiliencePipeline)
                    {
                        // See resilience handler below for timeout control
                        httpClient.Timeout = Timeout.InfiniteTimeSpan;
                    }
                }
            );

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

        services.AddSingleton<Func<HttpClient, GovukNotifyOptions, IAsyncNotificationClient>>(
            (httpClient, options) => new NotificationClient(new HttpClientWrapper(httpClient), options.ApiKey)
        );

        return services;
    }
}
