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
        var valid = options.Value.TryReadCutover(out var cutover);
        var mode = (valid, cutover) switch
        {
            (false, _) => "invalid",
            (_, null) => "send-all",
            _ => "boundary",
        };

        return Task.FromResult(
            HealthCheckResult.Healthy(
                data: new Dictionary<string, object>
                {
                    ["emailDeliveryCutoverUtc"] = cutover?.ToString("O", CultureInfo.InvariantCulture)!,
                    ["cutoverValid"] = valid,
                    ["mode"] = mode,
                }
            )
        );
    }
}
