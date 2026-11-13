using Defra.WasteObligations.Api.Utils.Http;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Services.Packaging;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPackagingService(
        this IServiceCollection services,
        bool addResiliencePipeline = true
    )
    {
        const string name = PackagingOptions.SectionName;
        services.AddOptions<PackagingOptions>().BindConfiguration(name).ValidateDataAnnotations();
        services.AddOptions<HttpStandardResilienceOptions>(name).BindConfiguration(name);
        services
            .AddHttpClient<IPackagingSource, PackagingSource>()
            .ConfigureHttpClient(
                (sp, httpClient) =>
                {
                    sp.GetRequiredService<IOptions<PackagingOptions>>().Value.Configure(httpClient);
                    httpClient.ConfigureForResiliencePipeline(addResiliencePipeline);
                }
            )
            .AddResiliencePipeline(addResiliencePipeline, name);

        return services;
    }
}
