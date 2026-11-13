using Microsoft.Extensions.Logging;

namespace Defra.WasteObligations.AuditEvents.Logging;

public static class CorrelationContext
{
    public const string PropertyName = "TraceId";

    private static readonly AsyncLocal<string?> TraceId = new();

    public static string? Current => TraceId.Value;

    public static IDisposable Begin(ILogger logger, string? id)
    {
        var current = TraceId.Value;
        var correlationId = string.IsNullOrWhiteSpace(id) ? null : id;
        var logScope =
            correlationId is null && current is null
                ? null
                : logger.BeginScope(new Dictionary<string, object?> { [PropertyName] = correlationId });
        TraceId.Value = correlationId;

        return new CorrelationScope(() => TraceId.Value = current, logScope);
    }
}
