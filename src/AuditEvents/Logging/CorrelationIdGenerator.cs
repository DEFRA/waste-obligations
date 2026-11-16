using System.Diagnostics;

namespace Defra.WasteObligations.AuditEvents.Logging;

public static class CorrelationIdGenerator
{
    public static string Generate() => ActivityTraceId.CreateRandom().ToString();
}
