using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using Amazon.CloudWatch.EMF.Model;

namespace Defra.WasteObligations.Api.Utils.Metrics;

[ExcludeFromCodeCoverage]
public class OrganisationObligationHydrationMetrics : IOrganisationObligationHydrationMetrics
{
    private readonly Counter<long> _failures;
    private readonly Histogram<long> _activeSummaryCount;
    private readonly Histogram<long> _dueSummaryCount;
    private readonly Histogram<double> _obligationReadDuration;
    private readonly Counter<long> _obligationReadFailures;
    private readonly Histogram<double> _staleSummaryAge;
    private readonly Histogram<long> _staleSummaryCount;
    private readonly Counter<long> _successes;

    public OrganisationObligationHydrationMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(Metrics.MeterName);

        _failures = meter.CreateCounter<long>(
            Metrics.Names.OrganisationObligationHydrationFailure,
            nameof(Unit.COUNT),
            "Count of organisation obligation hydration failures"
        );
        _activeSummaryCount = meter.CreateHistogram<long>(
            Metrics.Names.OrganisationObligationHydrationActiveSummaryCount,
            nameof(Unit.COUNT),
            "Count of active organisation obligation hydration summaries"
        );
        _dueSummaryCount = meter.CreateHistogram<long>(
            Metrics.Names.OrganisationObligationHydrationDueSummaryCount,
            nameof(Unit.COUNT),
            "Count of organisation obligation hydration summaries due for an obligations read"
        );
        _obligationReadDuration = meter.CreateHistogram<double>(
            Metrics.Names.OrganisationObligationHydrationObligationReadDuration,
            nameof(Unit.SECONDS),
            "Duration of organisation obligations reads"
        );
        _obligationReadFailures = meter.CreateCounter<long>(
            Metrics.Names.OrganisationObligationHydrationObligationReadFailure,
            nameof(Unit.COUNT),
            "Count of failed organisation obligations reads"
        );
        _staleSummaryAge = meter.CreateHistogram<double>(
            Metrics.Names.OrganisationObligationHydrationStaleSummaryAge,
            nameof(Unit.SECONDS),
            "Age of the oldest active stale organisation obligation summary"
        );
        _staleSummaryCount = meter.CreateHistogram<long>(
            Metrics.Names.OrganisationObligationHydrationStaleSummaryCount,
            nameof(Unit.COUNT),
            "Count of active stale organisation obligation summaries"
        );
        _successes = meter.CreateCounter<long>(
            Metrics.Names.OrganisationObligationHydrationSuccess,
            nameof(Unit.COUNT),
            "Count of successful organisation obligation summaries saved"
        );
    }

    public void Failed()
    {
        _failures.Add(1, BuildTags());
    }

    public void ObligationReadCompleted(TimeSpan duration)
    {
        _obligationReadDuration.Record(duration.TotalSeconds, BuildTags());
    }

    public void ObligationReadFailed(TimeSpan duration)
    {
        _obligationReadFailures.Add(1, BuildTags());
        _obligationReadDuration.Record(duration.TotalSeconds, BuildTags());
    }

    public void QueueObserved(int activeSummaryCount, int dueSummaryCount)
    {
        var tags = BuildTags();
        _activeSummaryCount.Record(activeSummaryCount, tags);
        _dueSummaryCount.Record(dueSummaryCount, tags);
    }

    public void Succeeded()
    {
        _successes.Add(1, BuildTags());
    }

    public void StalenessObserved(int staleSummaryCount, double oldestStaleSummaryAgeSeconds)
    {
        var tags = BuildTags();

        _staleSummaryCount.Record(staleSummaryCount, tags);
        if (staleSummaryCount > 0)
        {
            _staleSummaryAge.Record(oldestStaleSummaryAgeSeconds, tags);
        }
    }

    private static TagList BuildTags() => new() { { Metrics.Tags.Service, Process.GetCurrentProcess().ProcessName } };
}
