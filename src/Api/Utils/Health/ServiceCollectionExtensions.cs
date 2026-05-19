using System.Diagnostics.CodeAnalysis;
using Defra.WasteObligations.Api.Services.AccountBackend;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

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
            )
            .Add(
                new HealthCheckRegistration(
                    AccountBackendOptions.SectionName,
                    sp => new AccountBackendHealthCheck(sp),
                    HealthStatus.Unhealthy,
                    tags: [WebApplicationExtensions.Extended],
                    timeout: TimeSpan.FromSeconds(10)
                )
            )
            .Add(
                new HealthCheckRegistration(
                    WasteOrganisationsOptions.SectionName,
                    sp => new WasteOrganisationsHealthCheck(
                        sp.GetRequiredService<IOptions<WasteOrganisationsOptions>>().Value
                    ),
                    HealthStatus.Unhealthy,
                    tags: [WebApplicationExtensions.Extended],
                    timeout: TimeSpan.FromSeconds(10)
                )
            );

        return services;
    }
}
