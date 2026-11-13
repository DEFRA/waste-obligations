using System.Diagnostics.Metrics;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using Defra.WasteObligations.Api.Utils.Metrics;
using Microsoft.Extensions.DependencyInjection;
using ApiMetrics = Defra.WasteObligations.Api.Utils.Metrics.Metrics;

namespace Defra.WasteObligations.Api.Tests.Utils.Metrics;

public class ObligationCalculationMetricsTests
{
    [Fact]
    public void Measurements_HaveBoundedOutcomesAndCloudWatchUnits()
    {
        var units = new Dictionary<string, string?>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, _) =>
        {
            if (instrument.Meter.Name == ApiMetrics.MeterName && instrument.Name.StartsWith("ObligationCalculation"))
                units[instrument.Name] = instrument.Unit;
        };
        listener.Start();
        using var submitters = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            "ObligationCalculationSubmitterOutcome"
        );
        using var instructions = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            "ObligationCalculationInstructionOutcome"
        );
        using var duration = new TestMetricCollector<double>(
            ApiMetrics.MeterName,
            "ObligationCalculationPackagingFetchDuration"
        );
        using var services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var subject = new ObligationCalculationMetrics(services.GetRequiredService<IMeterFactory>());
        foreach (var outcome in Enum.GetValues<CalculationSaveOutcome>())
            subject.Submitter(outcome);
        subject.SubmitterFailed();
        foreach (var outcome in Enum.GetValues<CalculationInstructionOutcome>())
            subject.Instruction(outcome);
        subject.FetchDuration(TimeSpan.FromSeconds(2.5));

        submitters
            .GetMeasurementSnapshot()
            .Select(x => x.Tags[ApiMetrics.Tags.Outcome])
            .Should()
            .Equal("created", "updated", "unchanged", "failed");
        instructions
            .GetMeasurementSnapshot()
            .Select(x => x.Tags[ApiMetrics.Tags.Outcome])
            .Should()
            .Equal("success", "submitter_errors", "packaging_failure");
        submitters
            .GetMeasurementSnapshot()
            .Concat(instructions.GetMeasurementSnapshot())
            .Should()
            .OnlyContain(x => x.Value == 1 && x.Tags.Count == 1);
        duration.GetMeasurementSnapshot().Should().ContainSingle().Which.Value.Should().Be(2.5);
        duration.GetMeasurementSnapshot().Single().Tags.Should().BeEmpty();
        units["ObligationCalculationSubmitterOutcome"].Should().Be("COUNT");
        units["ObligationCalculationInstructionOutcome"].Should().Be("COUNT");
        units["ObligationCalculationPackagingFetchDuration"].Should().Be("SECONDS");
    }
}
