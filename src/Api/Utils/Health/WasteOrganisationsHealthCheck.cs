using System.Diagnostics.CodeAnalysis;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Defra.WasteObligations.Api.Utils.Health;

[ExcludeFromCodeCoverage]
public class WasteOrganisationsHealthCheck(WasteOrganisationsOptions options) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            using var httpClient = new HttpClient();

            options.Configure(httpClient);

            const string health = "health/authorized";
            var response = await httpClient.GetAsync(health, cancellationToken);

            response.EnsureSuccessStatusCode();

            return HealthCheckResult.Healthy($"Connected to {httpClient.BaseAddress}{health}");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                exception: new Exception($"Failed to connect to {WasteOrganisationsOptions.SectionName}", ex)
            );
        }
    }
}
