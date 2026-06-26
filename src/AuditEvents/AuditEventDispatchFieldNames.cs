using Defra.WasteObligations.AuditEvents.Entities;

namespace Defra.WasteObligations.AuditEvents;

internal static class AuditEventDispatchFieldNames
{
    private static readonly string Dispatches = ToCamelCase(nameof(AuditEvent.Dispatches));

    public static string DispatchPath(string processName) => $"{Dispatches}.{processName}";

    private static string ToCamelCase(string value) => char.ToLowerInvariant(value[0]) + value[1..];
}
