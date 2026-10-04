using System.Globalization;
using Defra.WasteObligations.Api.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Utils.Health;

public sealed class EmailDeliveryCutoverHealthCheck(IOptions<EmailDeliveryOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        options.Value.TryReadCutover(out var cutover);
        var mode = cutover is null ? "send-all" : "boundary";

        return Task.FromResult(
            HealthCheckResult.Healthy(
                data: new Dictionary<string, object>
                {
                    ["emailDeliveryCutoverUtc"] = cutover?.ToString("O", CultureInfo.InvariantCulture)!,
                    ["mode"] = mode,
                }
            )
        );
    }
}
