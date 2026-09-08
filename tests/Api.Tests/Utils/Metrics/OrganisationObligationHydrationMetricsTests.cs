using System.Diagnostics.Metrics;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Utils.Metrics;
using Microsoft.Extensions.DependencyInjection;
using ApiMetrics = Defra.WasteObligations.Api.Utils.Metrics.Metrics;

namespace Defra.WasteObligations.Api.Tests.Utils.Metrics;

public class OrganisationObligationHydrationMetricsTests
{
    [Fact]
    public void FailedAndSucceeded_ShouldIncrementOutcomeCounters()
    {
        var meterFactory = CreateMeterFactory();
        using var failureCollector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.OrganisationObligationHydrationFailure
        );
        using var successCollector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.OrganisationObligationHydrationSuccess
        );
        var subject = new OrganisationObligationHydrationMetrics(meterFactory);

        subject.Failed();
        subject.Succeeded();

        var failureMeasurements = failureCollector.GetMeasurementSnapshot();
        failureMeasurements.Should().ContainSingle();
        failureMeasurements[0].Value.Should().Be(1);
        var successMeasurements = successCollector.GetMeasurementSnapshot();
        successMeasurements.Should().ContainSingle();
        successMeasurements[0].Value.Should().Be(1);
    }

    [Fact]
    public void StalenessObserved_ShouldRecordStaleSummaryCountAndOldestAge()
    {
        var meterFactory = CreateMeterFactory();
        using var ageCollector = new TestMetricCollector<double>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.OrganisationObligationHydrationStaleSummaryAge
        );
        using var countCollector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.OrganisationObligationHydrationStaleSummaryCount
        );
        var subject = new OrganisationObligationHydrationMetrics(meterFactory);

        subject.StalenessObserved(2, 3600);

        var ageMeasurements = ageCollector.GetMeasurementSnapshot();
        ageMeasurements.Should().ContainSingle();
        ageMeasurements[0].Value.Should().Be(3600);
        var countMeasurements = countCollector.GetMeasurementSnapshot();
        countMeasurements.Should().ContainSingle();
        countMeasurements[0].Value.Should().Be(2);
    }

    [Fact]
    public void LeaseNotAcquired_ShouldRecordSkippedHydration()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.OrganisationObligationHydrationLeaseNotAcquired
        );
        var subject = new OrganisationObligationHydrationMetrics(meterFactory);

        subject.LeaseNotAcquired();

        collector.GetMeasurementSnapshot().Should().ContainSingle().Which.Value.Should().Be(1);
    }

    [Fact]
    public void ObligationReadAndQueueObserved_ShouldRecordDurationFailureAndQueueCounts()
    {
        var meterFactory = CreateMeterFactory();
        using var durationCollector = new TestMetricCollector<double>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.OrganisationObligationHydrationObligationReadDuration
        );
        using var failureCollector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.OrganisationObligationHydrationObligationReadFailure
        );
        using var activeCollector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.OrganisationObligationHydrationActiveSummaryCount
        );
        using var dueCollector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.OrganisationObligationHydrationDueSummaryCount
        );
        var subject = new OrganisationObligationHydrationMetrics(meterFactory);

        subject.ObligationReadCompleted(TimeSpan.FromSeconds(0.4));
        subject.ObligationReadFailed(TimeSpan.FromSeconds(1.2));
        subject.QueueObserved(4245, 4019);

        durationCollector.GetMeasurementSnapshot().Select(x => x.Value).Should().BeEquivalentTo([0.4d, 1.2d]);
        failureCollector.GetMeasurementSnapshot().Should().ContainSingle().Which.Value.Should().Be(1);
        activeCollector.GetMeasurementSnapshot().Should().ContainSingle().Which.Value.Should().Be(4245);
        dueCollector.GetMeasurementSnapshot().Should().ContainSingle().Which.Value.Should().Be(4019);
    }

    [Fact]
    public void CapacityObserved_ShouldRecordMinimumFullRefreshDurationAndConfiguration()
    {
        var meterFactory = CreateMeterFactory();
        using var minimumFullRefreshDurationCollector = new TestMetricCollector<double>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.OrganisationObligationHydrationMinimumFullRefreshDuration
        );
        using var refreshIntervalCollector = new TestMetricCollector<double>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.OrganisationObligationHydrationRefreshInterval
        );
        using var maximumRequestsCollector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.OrganisationObligationHydrationMaxDownstreamRequestsPerMinute
        );
        var subject = new OrganisationObligationHydrationMetrics(meterFactory);

        subject.CapacityObserved(4245, 200, TimeSpan.FromMinutes(30));

        minimumFullRefreshDurationCollector
            .GetMeasurementSnapshot()
            .Should()
            .ContainSingle()
            .Which.Value.Should()
            .Be(TimeSpan.FromMinutes(21.225).TotalSeconds);
        refreshIntervalCollector
            .GetMeasurementSnapshot()
            .Should()
            .ContainSingle()
            .Which.Value.Should()
            .Be(TimeSpan.FromMinutes(30).TotalSeconds);
        maximumRequestsCollector.GetMeasurementSnapshot().Should().ContainSingle().Which.Value.Should().Be(200);
    }

    private static IMeterFactory CreateMeterFactory()
    {
        var services = new ServiceCollection();
        services.AddMetrics();

        return services.BuildServiceProvider().GetRequiredService<IMeterFactory>();
    }
}
