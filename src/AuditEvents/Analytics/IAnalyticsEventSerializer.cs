namespace Defra.WasteObligations.AuditEvents.Analytics;

public interface IAnalyticsEventSerializer
{
    string Serialize(AnalyticsEvent analyticsEvent);
}
