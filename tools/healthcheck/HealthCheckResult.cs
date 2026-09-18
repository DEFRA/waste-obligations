namespace Defra.WasteObligations.HealthCheck;

public sealed record HealthCheckResult(
    string Name,
    bool Healthy,
    string Status,
    int? HttpStatus,
    long DurationMs,
    IReadOnlyDictionary<string, string> Checks
);
