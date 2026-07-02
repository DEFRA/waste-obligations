namespace Defra.WasteObligations.AuditEvents.Analytics;

public interface IAnalyticsEventSender
{
    Task Send(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken);
}
