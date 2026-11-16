namespace Defra.WasteObligations.AuditEvents.Analytics;

internal sealed record AnalyticsEntityRegistration(
    string LegacyIdentityPrefix,
    IReadOnlyDictionary<string, HashSet<string>> EventTypesByOperation
);
