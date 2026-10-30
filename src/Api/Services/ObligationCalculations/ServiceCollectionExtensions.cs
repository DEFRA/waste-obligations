using Microsoft.Extensions.DependencyInjection;

namespace Defra.WasteObligations.Api.Services.ObligationCalculations;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddObligationCalculationProcessing(
        this IServiceCollection services,
        bool addWorker = true
    )
    {
        services
            .AddOptions<ObligationCalculationProcessingOptions>()
            .BindConfiguration(ObligationCalculationProcessingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<IObligationCalculationInstructionProcessor, ObligationCalculationInstructionProcessor>();

        if (addWorker)
            services.AddHostedService<ObligationCalculationProcessingWorker>();

        return services;
    }
}
