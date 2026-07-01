using Defra.WasteObligations.AuditEvents.Entities;

namespace Defra.WasteObligations.AuditEvents;

internal static class AuditEventDispatchFieldNames
{
    private static readonly string Dispatches = ToCamelCase(nameof(AuditEvent.Dispatches));
    private static readonly string Date = ToCamelCase(nameof(AuditEventDispatch.Date));
    private static readonly string Status = ToCamelCase(nameof(AuditEventDispatch.Status));

    public static string DispatchPath(string processName) => $"{Dispatches}.{processName}";

    public static string DispatchDatePath(string processName) => $"{DispatchPath(processName)}.{Date}";

    public static string DispatchStatusPath(string processName) => $"{DispatchPath(processName)}.{Status}";

    private static string ToCamelCase(string value) => char.ToLowerInvariant(value[0]) + value[1..];
}
