using Defra.WasteObligations.AuditEvents.Analytics;

namespace Defra.WasteObligations.AuditEvents.Metrics;

public interface IAuditEventMetrics
{
    void SnsPublishStarted(string processName, string topicArn, AnalyticsEvent analyticsEvent);

    void SnsPublishCompleted(
        string processName,
        string topicArn,
        AnalyticsEvent analyticsEvent,
        double publishMilliseconds,
        double millisecondsSinceRecordedAt
    );

    void SnsPublishFaulted(string processName, string topicArn, AnalyticsEvent analyticsEvent, Exception exception);
}
