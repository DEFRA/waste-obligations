using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using Amazon.CloudWatch.EMF.Model;
using Defra.WasteObligations.AuditEvents.Analytics;
using Defra.WasteObligations.AuditEvents.Entities;
using Defra.WasteObligations.AuditEvents.Metrics;

namespace Defra.WasteObligations.Api.Utils.Metrics;

[ExcludeFromCodeCoverage]
public class AuditEventMetrics : IAuditEventMetrics
{
    private const string LeaseAcquired = "Acquired";
    private const string LeaseNotAcquired = "NotAcquired";
    private const string LeaseRenewalFailed = "RenewalFailed";

    private readonly Histogram<double> _dispatchPollDuration;
    private readonly Counter<long> _dispatchPoll;
    private readonly Counter<long> _dispatchPollErrors;
    private readonly Counter<long> _dispatchPollActive;
    private readonly Counter<long> _dispatchRead;
    private readonly Histogram<long> _dispatchBatchSize;
    private readonly Histogram<double> _dispatchLag;
    private readonly Counter<long> _dispatchOutcome;
    private readonly Counter<long> _dispatchMarkFailures;
    private readonly Counter<long> _dispatchLease;
    private readonly Histogram<double> _snsPublishDuration;
    private readonly Counter<long> _snsPublish;
    private readonly Counter<long> _snsPublishErrors;
    private readonly Counter<long> _snsPublishActive;
    private readonly Histogram<double> _snsPublishLatency;

    public AuditEventMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(Metrics.MeterName);

        _dispatchPollDuration = meter.CreateHistogram<double>(
            Metrics.Names.AuditEventDispatchPollDuration,
            nameof(Unit.MILLISECONDS),
            "Elapsed time spent polling audit events for dispatch"
        );
        _dispatchPoll = meter.CreateCounter<long>(
            Metrics.Names.AuditEventDispatchPoll,
            nameof(Unit.COUNT),
            "Count of audit event dispatch polls"
        );
        _dispatchPollErrors = meter.CreateCounter<long>(
            Metrics.Names.AuditEventDispatchPollErrors,
            nameof(Unit.COUNT),
            "Count of audit event dispatch poll failures"
        );
        _dispatchPollActive = meter.CreateCounter<long>(
            Metrics.Names.AuditEventDispatchPollActive,
            nameof(Unit.COUNT),
            "Count of audit event dispatch polls in progress"
        );
        _dispatchRead = meter.CreateCounter<long>(
            Metrics.Names.AuditEventDispatchRead,
            nameof(Unit.COUNT),
            "Count of audit events read for dispatch"
        );
        _dispatchBatchSize = meter.CreateHistogram<long>(
            Metrics.Names.AuditEventDispatchBatchSize,
            nameof(Unit.COUNT),
            "Number of audit events read in each dispatch batch"
        );
        _dispatchLag = meter.CreateHistogram<double>(
            Metrics.Names.AuditEventDispatchLag,
            nameof(Unit.MILLISECONDS),
            "Age of the oldest audit event read for dispatch"
        );
        _dispatchOutcome = meter.CreateCounter<long>(
            Metrics.Names.AuditEventDispatchOutcome,
            nameof(Unit.COUNT),
            "Count of audit event dispatch outcomes"
        );
        _dispatchMarkFailures = meter.CreateCounter<long>(
            Metrics.Names.AuditEventDispatchMarkFailures,
            nameof(Unit.COUNT),
            "Count of audit event dispatch outcomes that could not be marked"
        );
        _dispatchLease = meter.CreateCounter<long>(
            Metrics.Names.AuditEventDispatchLease,
            nameof(Unit.COUNT),
            "Count of audit event dispatch lease outcomes"
        );
        _snsPublishDuration = meter.CreateHistogram<double>(
            Metrics.Names.AuditEventSnsPublishDuration,
            nameof(Unit.MILLISECONDS),
            "Elapsed time spent publishing an audit event to SNS"
        );
        _snsPublish = meter.CreateCounter<long>(
            Metrics.Names.AuditEventSnsPublish,
            nameof(Unit.COUNT),
            "Count of audit event SNS publish attempts"
        );
        _snsPublishErrors = meter.CreateCounter<long>(
            Metrics.Names.AuditEventSnsPublishErrors,
            nameof(Unit.COUNT),
            "Count of audit event SNS publish failures"
        );
        _snsPublishActive = meter.CreateCounter<long>(
            Metrics.Names.AuditEventSnsPublishActive,
            nameof(Unit.COUNT),
            "Count of audit event SNS publish operations in progress"
        );
        _snsPublishLatency = meter.CreateHistogram<double>(
            Metrics.Names.AuditEventSnsPublishLatency,
            nameof(Unit.MILLISECONDS),
            "Elapsed time from audit event recording to SNS publish"
        );
    }

    public void DispatchPollStarted(string processName)
    {
        var tagList = BuildProcessTags(processName);

        _dispatchPoll.Add(1, tagList);
        _dispatchPollActive.Add(1, tagList);
    }

    public void DispatchPollCompleted(string processName, double milliseconds)
    {
        var tagList = BuildProcessTags(processName);

        _dispatchPollActive.Add(-1, tagList);
        _dispatchPollDuration.Record(milliseconds, tagList);
    }

    public void DispatchPollFaulted(string processName, Exception exception)
    {
        var tagList = BuildProcessTags(processName);
        tagList.Add(Metrics.Tags.ExceptionType, exception.GetType().Name);

        _dispatchPollErrors.Add(1, tagList);
    }

    public void DispatchBatchRead(string processName, int count, double? oldestUnsentMilliseconds)
    {
        var tagList = BuildProcessTags(processName);

        _dispatchRead.Add(count, tagList);
        _dispatchBatchSize.Record(count, tagList);

        if (oldestUnsentMilliseconds.HasValue)
            _dispatchLag.Record(oldestUnsentMilliseconds.Value, tagList);
    }

    public void DispatchLeaseAcquired(string processName) => DispatchLease(processName, LeaseAcquired);

    public void DispatchLeaseNotAcquired(string processName) => DispatchLease(processName, LeaseNotAcquired);

    public void DispatchLeaseRenewalFailed(string processName) => DispatchLease(processName, LeaseRenewalFailed);

    public void DispatchDispatched(string processName, AuditEvent auditEvent) =>
        DispatchOutcome(processName, auditEvent, AuditEventDispatchStatus.Dispatched);

    public void DispatchFailed(
        string processName,
        AuditEvent auditEvent,
        AuditEventDispatchStatus status,
        Exception exception
    )
    {
        var tagList = BuildAuditEventTags(processName, auditEvent);
        tagList.Add(Metrics.Tags.DispatchStatus, status.ToString());
        tagList.Add(Metrics.Tags.ExceptionType, exception.GetType().Name);

        _dispatchOutcome.Add(1, tagList);
    }

    public void DispatchMarkFailed(string processName, AuditEvent auditEvent, string outcome)
    {
        var tagList = BuildAuditEventTags(processName, auditEvent);
        tagList.Add(Metrics.Tags.DispatchOutcome, outcome);

        _dispatchMarkFailures.Add(1, tagList);
    }

    public void SnsPublishStarted(string processName, string topicArn, AnalyticsEvent analyticsEvent)
    {
        var tagList = BuildTags(processName, topicArn, analyticsEvent);

        _snsPublish.Add(1, tagList);
        _snsPublishActive.Add(1, tagList);
    }

    public void SnsPublishCompleted(
        string processName,
        string topicArn,
        AnalyticsEvent analyticsEvent,
        double publishMilliseconds,
        double millisecondsSinceRecordedAt
    )
    {
        var tagList = BuildTags(processName, topicArn, analyticsEvent);

        _snsPublishActive.Add(-1, tagList);
        _snsPublishDuration.Record(publishMilliseconds, tagList);
        _snsPublishLatency.Record(millisecondsSinceRecordedAt, tagList);
    }

    public void SnsPublishFaulted(
        string processName,
        string topicArn,
        AnalyticsEvent analyticsEvent,
        Exception exception
    )
    {
        var tagList = BuildTags(processName, topicArn, analyticsEvent);
        tagList.Add(Metrics.Tags.ExceptionType, exception.GetType().Name);

        _snsPublishErrors.Add(1, tagList);
    }

    private void DispatchLease(string processName, string outcome)
    {
        var tagList = BuildProcessTags(processName);
        tagList.Add(Metrics.Tags.LeaseOutcome, outcome);

        _dispatchLease.Add(1, tagList);
    }

    private void DispatchOutcome(string processName, AuditEvent auditEvent, AuditEventDispatchStatus status)
    {
        var tagList = BuildAuditEventTags(processName, auditEvent);
        tagList.Add(Metrics.Tags.DispatchStatus, status.ToString());

        _dispatchOutcome.Add(1, tagList);
    }

    private static TagList BuildProcessTags(string processName) =>
        new()
        {
            { Metrics.Tags.Service, Process.GetCurrentProcess().ProcessName },
            { Metrics.Tags.ProcessName, processName },
        };

    private static TagList BuildAuditEventTags(string processName, AuditEvent auditEvent) =>
        new()
        {
            { Metrics.Tags.Service, Process.GetCurrentProcess().ProcessName },
            { Metrics.Tags.ProcessName, processName },
            { Metrics.Tags.Entity, auditEvent.Entity },
            { Metrics.Tags.Operation, auditEvent.Operation },
            { Metrics.Tags.EventType, auditEvent.EventType },
        };

    private static TagList BuildTags(string processName, string topicArn, AnalyticsEvent analyticsEvent) =>
        new()
        {
            { Metrics.Tags.Service, Process.GetCurrentProcess().ProcessName },
            { Metrics.Tags.ProcessName, processName },
            { Metrics.Tags.TopicName, ToTopicName(topicArn) },
            { Metrics.Tags.Entity, analyticsEvent.Entity },
            { Metrics.Tags.Operation, analyticsEvent.Operation },
            { Metrics.Tags.EventType, analyticsEvent.EventType },
        };

    private static string ToTopicName(string topicArn)
    {
        const char arnSeparator = ':';
        var separatorIndex = topicArn.LastIndexOf(arnSeparator);

        return separatorIndex >= 0 && separatorIndex < topicArn.Length - 1
            ? topicArn[(separatorIndex + 1)..]
            : topicArn;
    }
}
