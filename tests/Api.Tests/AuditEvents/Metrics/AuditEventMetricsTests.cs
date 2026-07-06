using System.Diagnostics.Metrics;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Tests.Utils.Metrics;
using Defra.WasteObligations.AuditEvents.Entities;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Microsoft.Extensions.DependencyInjection;
using ApiAuditEventMetrics = Defra.WasteObligations.Api.Utils.Metrics.AuditEventMetrics;
using ApiMetrics = Defra.WasteObligations.Api.Utils.Metrics.Metrics;

namespace Defra.WasteObligations.Api.Tests.AuditEvents.Metrics;

public class AuditEventMetricsTests
{
    private const string ProcessName = "analytics";
    private const string TopicArn = "arn:aws:sns:eu-west-2:000000000000:waste_obligations_analytics_events";
    private const string TopicName = "waste_obligations_analytics_events";

    [Fact]
    public void DispatchPollStarted_ShouldIncrementPollAndActiveCounters()
    {
        var meterFactory = CreateMeterFactory();
        using var pollCollector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.AuditEventDispatchPoll
        );
        using var activeCollector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.AuditEventDispatchPollActive
        );
        var subject = new ApiAuditEventMetrics(meterFactory);

        subject.DispatchPollStarted(ProcessName);

        var pollMeasurements = pollCollector.GetMeasurementSnapshot();
        pollMeasurements.Should().ContainSingle();
        pollMeasurements[0].Value.Should().Be(1);
        pollMeasurements[0].Tags[ApiMetrics.Tags.ProcessName].Should().Be(ProcessName);

        var activeMeasurements = activeCollector.GetMeasurementSnapshot();
        activeMeasurements.Should().ContainSingle();
        activeMeasurements[0].Value.Should().Be(1);
    }

    [Fact]
    public void DispatchPollCompleted_ShouldDecrementActiveCounterAndRecordDuration()
    {
        const double pollMilliseconds = 321;

        var meterFactory = CreateMeterFactory();
        using var activeCollector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.AuditEventDispatchPollActive
        );
        using var durationCollector = new TestMetricCollector<double>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.AuditEventDispatchPollDuration
        );
        var subject = new ApiAuditEventMetrics(meterFactory);

        subject.DispatchPollCompleted(ProcessName, pollMilliseconds);

        var activeMeasurements = activeCollector.GetMeasurementSnapshot();
        activeMeasurements.Should().ContainSingle();
        activeMeasurements[0].Value.Should().Be(-1);

        var durationMeasurements = durationCollector.GetMeasurementSnapshot();
        durationMeasurements.Should().ContainSingle();
        durationMeasurements[0].Value.Should().Be(pollMilliseconds);
        durationMeasurements[0].Tags[ApiMetrics.Tags.ProcessName].Should().Be(ProcessName);
    }

    [Fact]
    public void DispatchPollFaulted_ShouldIncrementFailureCounterWithExceptionType()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.AuditEventDispatchPollErrors
        );
        var subject = new ApiAuditEventMetrics(meterFactory);

        subject.DispatchPollFaulted(ProcessName, new InvalidOperationException("Failed"));

        var measurements = collector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle();
        measurements[0].Value.Should().Be(1);
        measurements[0].Tags[ApiMetrics.Tags.ExceptionType].Should().Be(nameof(InvalidOperationException));
    }

    [Fact]
    public void DispatchBatchRead_ShouldRecordReadCountBatchSizeAndLag()
    {
        const int count = 4;
        const double oldestUnsentMilliseconds = 1234;

        var meterFactory = CreateMeterFactory();
        using var readCollector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.AuditEventDispatchRead
        );
        using var batchSizeCollector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.AuditEventDispatchBatchSize
        );
        using var lagCollector = new TestMetricCollector<double>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.AuditEventDispatchLag
        );
        var subject = new ApiAuditEventMetrics(meterFactory);

        subject.DispatchBatchRead(ProcessName, count, oldestUnsentMilliseconds);

        var readMeasurements = readCollector.GetMeasurementSnapshot();
        readMeasurements.Should().ContainSingle();
        readMeasurements[0].Value.Should().Be(count);

        var batchSizeMeasurements = batchSizeCollector.GetMeasurementSnapshot();
        batchSizeMeasurements.Should().ContainSingle();
        batchSizeMeasurements[0].Value.Should().Be(count);

        var lagMeasurements = lagCollector.GetMeasurementSnapshot();
        lagMeasurements.Should().ContainSingle();
        lagMeasurements[0].Value.Should().Be(oldestUnsentMilliseconds);
    }

    [Fact]
    public void DispatchLeaseRenewalFailed_ShouldIncrementLeaseCounterWithOutcome()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.AuditEventDispatchLease
        );
        var subject = new ApiAuditEventMetrics(meterFactory);

        subject.DispatchLeaseRenewalFailed(ProcessName);

        var measurements = collector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle();
        measurements[0].Value.Should().Be(1);
        measurements[0].Tags[ApiMetrics.Tags.LeaseOutcome].Should().Be("RenewalFailed");
    }

    [Fact]
    public void DispatchDispatched_ShouldIncrementOutcomeCounterWithEventTags()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.AuditEventDispatchOutcome
        );
        var subject = new ApiAuditEventMetrics(meterFactory);
        var auditEvent = AuditEventFixture.ComplianceDeclaration("event-1", 1).Create();

        subject.DispatchDispatched(ProcessName, auditEvent);

        var measurements = collector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle();
        measurements[0].Value.Should().Be(1);
        measurements[0].Tags[ApiMetrics.Tags.DispatchStatus].Should().Be("Dispatched");
        measurements[0].Tags[ApiMetrics.Tags.Entity].Should().Be("compliance_declaration");
        measurements[0].Tags[ApiMetrics.Tags.Operation].Should().Be("insert");
        measurements[0].Tags[ApiMetrics.Tags.EventType].Should().Be("submission.created");
    }

    [Fact]
    public void DispatchFailed_ShouldIncrementOutcomeCounterWithStatusAndExceptionType()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.AuditEventDispatchOutcome
        );
        var subject = new ApiAuditEventMetrics(meterFactory);
        var auditEvent = AuditEventFixture.ComplianceDeclaration("event-1", 1).Create();

        subject.DispatchFailed(
            ProcessName,
            auditEvent,
            AuditEventDispatchStatus.DeadLettered,
            new InvalidOperationException("Failed")
        );

        var measurements = collector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle();
        measurements[0].Value.Should().Be(1);
        measurements[0].Tags[ApiMetrics.Tags.DispatchStatus].Should().Be("DeadLettered");
        measurements[0].Tags[ApiMetrics.Tags.ExceptionType].Should().Be(nameof(InvalidOperationException));
    }

    [Fact]
    public void DispatchMarkFailed_ShouldIncrementMarkFailureCounterWithOutcome()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.AuditEventDispatchMarkFailures
        );
        var subject = new ApiAuditEventMetrics(meterFactory);
        var auditEvent = AuditEventFixture.ComplianceDeclaration("event-1", 1).Create();

        subject.DispatchMarkFailed(ProcessName, auditEvent, "processed");

        var measurements = collector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle();
        measurements[0].Value.Should().Be(1);
        measurements[0].Tags[ApiMetrics.Tags.DispatchOutcome].Should().Be("processed");
    }

    [Fact]
    public void SnsPublishStarted_ShouldIncrementPublishCounter()
    {
        var meterFactory = CreateMeterFactory();
        using var publishCollector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.AuditEventSnsPublish
        );
        using var activeCollector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.AuditEventSnsPublishActive
        );
        var subject = new ApiAuditEventMetrics(meterFactory);
        var analyticsEvent = AnalyticsEventFixture.ComplianceDeclaration().Create();

        subject.SnsPublishStarted(ProcessName, TopicArn, analyticsEvent);

        var publishMeasurements = publishCollector.GetMeasurementSnapshot();
        publishMeasurements.Should().ContainSingle();
        publishMeasurements[0].Value.Should().Be(1);
        publishMeasurements[0].Tags[ApiMetrics.Tags.ProcessName].Should().Be(ProcessName);
        publishMeasurements[0].Tags[ApiMetrics.Tags.TopicName].Should().Be(TopicName);
        publishMeasurements[0].Tags[ApiMetrics.Tags.Entity].Should().Be("compliance_declaration");
        publishMeasurements[0].Tags[ApiMetrics.Tags.Operation].Should().Be("insert");
        publishMeasurements[0].Tags[ApiMetrics.Tags.EventType].Should().Be("submission.created");

        var activeMeasurements = activeCollector.GetMeasurementSnapshot();
        activeMeasurements.Should().ContainSingle();
        activeMeasurements[0].Value.Should().Be(1);
    }

    [Fact]
    public void SnsPublishCompleted_ShouldDecrementActiveCounterAndRecordDurations()
    {
        const double publishMilliseconds = 567;
        const double millisecondsSinceRecordedAt = 1234;

        var meterFactory = CreateMeterFactory();
        using var activeCollector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.AuditEventSnsPublishActive
        );
        using var durationCollector = new TestMetricCollector<double>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.AuditEventSnsPublishDuration
        );
        using var latencyCollector = new TestMetricCollector<double>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.AuditEventSnsPublishLatency
        );
        var subject = new ApiAuditEventMetrics(meterFactory);
        var analyticsEvent = AnalyticsEventFixture.ComplianceDeclaration().Create();

        subject.SnsPublishCompleted(
            ProcessName,
            TopicArn,
            analyticsEvent,
            publishMilliseconds,
            millisecondsSinceRecordedAt
        );

        var activeMeasurements = activeCollector.GetMeasurementSnapshot();
        activeMeasurements.Should().ContainSingle();
        activeMeasurements[0].Value.Should().Be(-1);

        var durationMeasurements = durationCollector.GetMeasurementSnapshot();
        durationMeasurements.Should().ContainSingle();
        durationMeasurements[0].Value.Should().Be(publishMilliseconds);
        durationMeasurements[0].Tags[ApiMetrics.Tags.Entity].Should().Be("compliance_declaration");

        var latencyMeasurements = latencyCollector.GetMeasurementSnapshot();
        latencyMeasurements.Should().ContainSingle();
        latencyMeasurements[0].Value.Should().Be(millisecondsSinceRecordedAt);
        latencyMeasurements[0].Tags[ApiMetrics.Tags.Entity].Should().Be("compliance_declaration");
    }

    [Fact]
    public void SnsPublishFaulted_ShouldIncrementFailureCounterWithExceptionType()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.AuditEventSnsPublishErrors
        );
        var subject = new ApiAuditEventMetrics(meterFactory);
        var analyticsEvent = AnalyticsEventFixture.ComplianceDeclaration().Create();

        subject.SnsPublishFaulted(ProcessName, TopicArn, analyticsEvent, new InvalidOperationException("Failed"));

        var measurements = collector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle();
        measurements[0].Value.Should().Be(1);
        measurements[0].Tags[ApiMetrics.Tags.ExceptionType].Should().Be(nameof(InvalidOperationException));
    }

    private static IMeterFactory CreateMeterFactory()
    {
        var services = new ServiceCollection();
        services.AddMetrics();

        return services.BuildServiceProvider().GetRequiredService<IMeterFactory>();
    }
}
