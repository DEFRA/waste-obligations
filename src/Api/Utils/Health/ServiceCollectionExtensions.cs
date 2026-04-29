using System.Diagnostics.CodeAnalysis;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Defra.WasteObligations.Api.Utils.Health;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHealth(this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .Add(
                new HealthCheckRegistration(
                    PrnCommonBackendOptions.SectionName,
                    sp => new PrnCommonBackendHealthCheck(sp),
                    HealthStatus.Unhealthy,
                    tags: [WebApplicationExtensions.Extended],
                    timeout: TimeSpan.FromSeconds(10)
                )
            );

        return services;
    }
}
