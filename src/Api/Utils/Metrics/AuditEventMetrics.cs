using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using Amazon.CloudWatch.EMF.Model;
using Defra.WasteObligations.AuditEvents.Analytics;
using Defra.WasteObligations.AuditEvents.Metrics;

namespace Defra.WasteObligations.Api.Utils.Metrics;

[ExcludeFromCodeCoverage]
public class AuditEventMetrics : IAuditEventMetrics
{
    private readonly Histogram<double> _snsPublishDuration;
    private readonly Counter<long> _snsPublish;
    private readonly Counter<long> _snsPublishErrors;
    private readonly Counter<long> _snsPublishActive;
    private readonly Histogram<double> _snsPublishLatency;

    public AuditEventMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(Metrics.MeterName);

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
