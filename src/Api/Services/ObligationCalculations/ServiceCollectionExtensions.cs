using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Services.ObligationCalculations;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddObligationCalculation(this IServiceCollection services)
    {
        services
            .AddOptions<ObligationCalculationOptions>()
            .BindConfiguration(ObligationCalculationOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<ObligationCalculationOptions>, ObligationCalculationOptionsValidator>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddTransient<IObligationCalculator, ObligationCalculator>();

        services.AddTransient<IObligationCalculationService, ObligationCalculationService>();

        return services;
    }
}
