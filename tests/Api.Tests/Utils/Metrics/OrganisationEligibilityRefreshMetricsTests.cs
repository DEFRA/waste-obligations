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

        outcomeCollector
            .GetMeasurementSnapshot()
            .Select(x => x.Tags[ApiMetrics.Tags.Outcome])
            .Should()
            .BeEquivalentTo(["Promoted", "Failed"]);
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

    [Fact]
    public void LeaseNotAcquired_ShouldRecordSkippedRefresh()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.OrganisationEligibilityRefreshLeaseNotAcquired
        );
        var subject = new OrganisationEligibilityRefreshMetrics(meterFactory);

        subject.LeaseNotAcquired();

        collector.GetMeasurementSnapshot().Should().ContainSingle().Which.Value.Should().Be(1);
    }

    [Fact]
    public void DownstreamCalls_ShouldRecordWasteOrganisationsAndAccountLookupMetrics()
    {
        var meterFactory = CreateMeterFactory();
        using var wasteOrganisationsDurationCollector = new TestMetricCollector<double>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.OrganisationEligibilityRefreshWasteOrganisationsReadDuration
        );
        using var wasteOrganisationsFailureCollector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.OrganisationEligibilityRefreshWasteOrganisationsReadFailure
        );
        using var wasteOrganisationsRowCountCollector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.OrganisationEligibilityRefreshWasteOrganisationsRowCount
        );
        using var accountDurationCollector = new TestMetricCollector<double>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.OrganisationEligibilityRefreshAccountReferenceLookupDuration
        );
        using var accountFailureCollector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.OrganisationEligibilityRefreshAccountReferenceLookupFailure
        );
        using var accountBatchSizeCollector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.OrganisationEligibilityRefreshAccountReferenceLookupBatchSize
        );
        var subject = new OrganisationEligibilityRefreshMetrics(meterFactory);

        subject.WasteOrganisationsReadCompleted(7000, TimeSpan.FromSeconds(1.2));
        subject.WasteOrganisationsReadFailed(TimeSpan.FromSeconds(2.4));
        subject.AccountReferenceLookupCompleted(RegistrationType.DirectProducer, 100, TimeSpan.FromSeconds(0.4));
        subject.AccountReferenceLookupFailed(RegistrationType.ComplianceScheme, TimeSpan.FromSeconds(0.8));

        wasteOrganisationsDurationCollector
            .GetMeasurementSnapshot()
            .Select(x => x.Value)
            .Should()
            .BeEquivalentTo([1.2d, 2.4d]);
        wasteOrganisationsFailureCollector.GetMeasurementSnapshot().Should().ContainSingle().Which.Value.Should().Be(1);
        wasteOrganisationsRowCountCollector
            .GetMeasurementSnapshot()
            .Should()
            .ContainSingle()
            .Which.Value.Should()
            .Be(7000);
        accountDurationCollector.GetMeasurementSnapshot().Select(x => x.Value).Should().BeEquivalentTo([0.4d, 0.8d]);
        accountFailureCollector.GetMeasurementSnapshot().Should().ContainSingle().Which.Value.Should().Be(1);
        accountBatchSizeCollector.GetMeasurementSnapshot().Should().ContainSingle().Which.Value.Should().Be(100);
        accountDurationCollector
            .GetMeasurementSnapshot()
            .Select(x => x.Tags[ApiMetrics.Tags.AccountReferenceLookupType])
            .Should()
            .BeEquivalentTo([RegistrationType.DirectProducer.ToString(), RegistrationType.ComplianceScheme.ToString()]);
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
