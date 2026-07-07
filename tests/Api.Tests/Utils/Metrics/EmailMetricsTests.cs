using System.Diagnostics.Metrics;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Utils.Metrics;
using Microsoft.Extensions.DependencyInjection;
using ApiMetrics = Defra.WasteObligations.Api.Utils.Metrics.Metrics;

namespace Defra.WasteObligations.Api.Tests.Utils.Metrics;

public class EmailMetricsTests
{
    private const string Language = "cy";
    private const string TemplateName = "ComplianceDeclarationSubmissionComplianceScheme";

    [Fact]
    public void SendStarted_ShouldIncrementSendAndActiveCounters()
    {
        var meterFactory = CreateMeterFactory();
        using var sendCollector = new TestMetricCollector<long>(ApiMetrics.MeterName, ApiMetrics.Names.EmailSend);
        using var activeCollector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.EmailSendActive
        );
        var subject = new EmailMetrics(meterFactory);

        subject.SendStarted(TemplateName, Language);

        var sendMeasurements = sendCollector.GetMeasurementSnapshot();
        sendMeasurements.Should().ContainSingle();
        sendMeasurements[0].Value.Should().Be(1);
        sendMeasurements[0].Tags[ApiMetrics.Tags.TemplateName].Should().Be(TemplateName);
        sendMeasurements[0].Tags[ApiMetrics.Tags.Language].Should().Be(Language);

        var activeMeasurements = activeCollector.GetMeasurementSnapshot();
        activeMeasurements.Should().ContainSingle();
        activeMeasurements[0].Value.Should().Be(1);
    }

    [Fact]
    public void SendCompleted_ShouldDecrementActiveCounterAndRecordDuration()
    {
        const double milliseconds = 432;

        var meterFactory = CreateMeterFactory();
        using var activeCollector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.EmailSendActive
        );
        using var durationCollector = new TestMetricCollector<double>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.EmailSendDuration
        );
        var subject = new EmailMetrics(meterFactory);

        subject.SendCompleted(TemplateName, Language, milliseconds);

        var activeMeasurements = activeCollector.GetMeasurementSnapshot();
        activeMeasurements.Should().ContainSingle();
        activeMeasurements[0].Value.Should().Be(-1);

        var durationMeasurements = durationCollector.GetMeasurementSnapshot();
        durationMeasurements.Should().ContainSingle();
        durationMeasurements[0].Value.Should().Be(milliseconds);
        durationMeasurements[0].Tags[ApiMetrics.Tags.TemplateName].Should().Be(TemplateName);
    }

    [Fact]
    public void SendFaulted_ShouldIncrementErrorCounterWithExceptionType()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TestMetricCollector<long>(ApiMetrics.MeterName, ApiMetrics.Names.EmailSendErrors);
        var subject = new EmailMetrics(meterFactory);

        subject.SendFaulted(TemplateName, Language, new InvalidOperationException("Failed"));

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
