using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

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
        services.AddOptions<WasteOrganisationsOptions>(name).BindConfiguration(name);

        var httpClientBuilder = services
            .AddHttpClient<IWasteOrganisationsService, WasteOrganisationsService>()
            .ConfigureHttpClient(
                (sp, httpClient) =>
                {
                    sp.GetRequiredService<IOptions<WasteOrganisationsOptions>>().Value.Configure(httpClient);

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
                nameof(IWasteOrganisationsService),
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
