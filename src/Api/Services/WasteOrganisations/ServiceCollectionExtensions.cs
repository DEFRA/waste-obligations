using Defra.WasteObligations.Api.Utils.Http;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Services.WasteOrganisations;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWasteOrganisationsService(
        this IServiceCollection services,
        bool addResiliencePipeline = true
    )
    {
        const string name = WasteOrganisationsOptions.SectionName;

        services.AddOptions<WasteOrganisationsOptions>().BindConfiguration(name).ValidateDataAnnotations();
        services.AddOptions<HttpStandardResilienceOptions>(name).BindConfiguration(name);

        services
            .AddHttpClient<IWasteOrganisationsService, WasteOrganisationsService>()
            .ConfigureHttpClient(
                (sp, httpClient) =>
                {
                    sp.GetRequiredService<IOptions<WasteOrganisationsOptions>>().Value.Configure(httpClient);
                    httpClient.ConfigureForResiliencePipeline(addResiliencePipeline);
                }
            )
            .AddHeaderPropagation()
            .AddResiliencePipeline(addResiliencePipeline, name);

        return services;
    }
}
