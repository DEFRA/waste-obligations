namespace Defra.WasteObligations.AuditEvents;

public interface IAnalyticsEventSender
{
    Task Send(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken);
}
