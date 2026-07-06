using Defra.WasteObligations.AuditEvents.Metrics;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.AuditEvents.Analytics;

public class MetricsAnalyticsEventSender(
    IAnalyticsEventSender innerSender,
    IAuditEventMetrics auditEventMetrics,
    IOptions<AnalyticsAuditEventProcessorOptions> options
) : IAnalyticsEventSender
{
    public async Task Send(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken)
    {
        var startingTimestamp = TimeProvider.System.GetTimestamp();
        var processName = options.Value.ProcessName;
        var topicArn = options.Value.TopicArn;
        auditEventMetrics.SnsPublishStarted(processName, topicArn, analyticsEvent);

        try
        {
            await innerSender.Send(analyticsEvent, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            auditEventMetrics.SnsPublishFaulted(processName, topicArn, analyticsEvent, exception);

            throw;
        }
        finally
        {
            auditEventMetrics.SnsPublishCompleted(
                processName,
                topicArn,
                analyticsEvent,
                TimeProvider.System.GetElapsedTime(startingTimestamp).TotalMilliseconds,
                MillisecondsSinceRecordedAt(analyticsEvent)
            );
        }
    }

    private static double MillisecondsSinceRecordedAt(AnalyticsEvent analyticsEvent)
    {
        var milliseconds = (TimeProvider.System.GetUtcNow() - analyticsEvent.RecordedAt).TotalMilliseconds;

        return Math.Max(milliseconds, 0);
    }
}
