using Defra.WasteObligations.AuditEvents.Analytics;
using Defra.WasteObligations.AuditEvents.Entities;

namespace Defra.WasteObligations.AuditEvents.Metrics;

public interface IAuditEventMetrics
{
    void DispatchPollStarted(string processName);

    void DispatchPollCompleted(string processName, double milliseconds);

    void DispatchPollFaulted(string processName, Exception exception);

    void DispatchBatchRead(string processName, int count, double? oldestUnsentMilliseconds);

    void DispatchLeaseAcquired(string processName);

    void DispatchLeaseNotAcquired(string processName);

    void DispatchLeaseRenewalFailed(string processName);

    void DispatchDispatched(string processName, AuditEvent auditEvent);

    void DispatchFailed(
        string processName,
        AuditEvent auditEvent,
        AuditEventDispatchStatus status,
        Exception exception
    );

    void DispatchMarkFailed(string processName, AuditEvent auditEvent, string outcome);

    void SqsPublishStarted(string processName, string queueUrl, AnalyticsEvent analyticsEvent);
    void SqsPublishCompleted(
        string processName,
        string queueUrl,
        AnalyticsEvent analyticsEvent,
        double publishMilliseconds,
        double millisecondsSinceRecordedAt
    );
    void SqsPublishFaulted(string processName, string queueUrl, AnalyticsEvent analyticsEvent, Exception exception);

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
