using System.Diagnostics.Metrics;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.OrganisationEligibility;
using Defra.WasteObligations.Api.Utils.Metrics;
using Microsoft.Extensions.DependencyInjection;
using ApiMetrics = Defra.WasteObligations.Api.Utils.Metrics.Metrics;

namespace Defra.WasteObligations.Api.Tests.Utils.Metrics;

public class OrganisationEligibilityRefreshMetricsTests
{
    [Fact]
    public void CompletedAndFailed_ShouldRecordRefreshOutcomesDurationAndRowCount()
    {
        var meterFactory = CreateMeterFactory();
        using var outcomeCollector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.OrganisationEligibilityRefreshOutcome
        );
        using var durationCollector = new TestMetricCollector<double>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.OrganisationEligibilityRefreshDuration
        );
        using var rowCountCollector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.OrganisationEligibilityRefreshRowCount
        );
        var subject = new OrganisationEligibilityRefreshMetrics(meterFactory);

        subject.Completed(
            new OrganisationEligibilityRefreshResult
            {
                Outcome = OrganisationEligibilityRefreshOutcome.Promoted,
                ActiveGeneration = "generation",
                RowCount = 4245,
                ContentFingerprint = "fingerprint",
            },
            TimeSpan.FromSeconds(12)
        );
        subject.Failed(TimeSpan.FromSeconds(3));
        subject.LeaseSkipped();

        outcomeCollector
            .GetMeasurementSnapshot()
            .Select(x => x.Tags[ApiMetrics.Tags.Outcome])
            .Should()
            .BeEquivalentTo(["Promoted", "Failed", "LeaseSkipped"]);
        durationCollector.GetMeasurementSnapshot().Select(x => x.Value).Should().BeEquivalentTo([12d, 3d]);
        rowCountCollector.GetMeasurementSnapshot().Should().ContainSingle().Which.Value.Should().Be(4245);
    }

    [Fact]
    public void ReferenceResolutionObserved_ShouldRecordEachResolutionState()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.OrganisationEligibilityReferenceResolutionCount
        );
        var subject = new OrganisationEligibilityRefreshMetrics(meterFactory);

        subject.ReferenceResolutionObserved([
            Row(OrganisationReferenceNumberResolutionState.Resolved),
            Row(OrganisationReferenceNumberResolutionState.Resolved),
            Row(OrganisationReferenceNumberResolutionState.Pending),
        ]);

        var measurements = collector.GetMeasurementSnapshot();
        measurements.Should().HaveCount(Enum.GetValues<OrganisationReferenceNumberResolutionState>().Length);
        measurements
            .Single(x => x.Tags[ApiMetrics.Tags.ReferenceResolutionState]?.ToString() == "Resolved")
            .Value.Should()
            .Be(2);
        measurements
            .Single(x => x.Tags[ApiMetrics.Tags.ReferenceResolutionState]?.ToString() == "Pending")
            .Value.Should()
            .Be(1);
    }

    private static OrganisationComplianceDeclarationEligibility Row(OrganisationReferenceNumberResolutionState state) =>
        new()
        {
            Generation = "generation",
            Name = "Example organisation",
            ReferenceNumberResolutionState = state,
            SourceFingerprint = "fingerprint",
        };

    private static IMeterFactory CreateMeterFactory()
    {
        var services = new ServiceCollection();
        services.AddMetrics();

        return services.BuildServiceProvider().GetRequiredService<IMeterFactory>();
    }
}
