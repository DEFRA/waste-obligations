using Defra.WasteObligations.Api.Utils.Http;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Services.WastePackaging;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWastePackagingService(
        this IServiceCollection services,
        bool addResiliencePipeline = true
    )
    {
        const string name = WastePackagingOptions.SectionName;
        services.AddOptions<WastePackagingOptions>().BindConfiguration(name).ValidateDataAnnotations();
        services.AddOptions<HttpStandardResilienceOptions>(name).BindConfiguration(name);
        services
            .AddHttpClient<IWastePackagingService, WastePackagingService>()
            .ConfigureHttpClient(
                (sp, httpClient) =>
                {
                    sp.GetRequiredService<IOptions<WastePackagingOptions>>().Value.Configure(httpClient);
                    httpClient.ConfigureForResiliencePipeline(addResiliencePipeline);
                }
            )
            .AddResiliencePipeline(addResiliencePipeline, name);

        return services;
    }
}
