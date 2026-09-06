using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using Amazon.CloudWatch.EMF.Model;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.OrganisationEligibility;

namespace Defra.WasteObligations.Api.Utils.Metrics;

[ExcludeFromCodeCoverage]
public class OrganisationEligibilityRefreshMetrics : IOrganisationEligibilityRefreshMetrics
{
    private readonly Histogram<double> _duration;
    private readonly Counter<long> _outcome;
    private readonly Histogram<long> _referenceResolutionCount;
    private readonly Histogram<long> _rowCount;

    public OrganisationEligibilityRefreshMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(Metrics.MeterName);

        _duration = meter.CreateHistogram<double>(
            Metrics.Names.OrganisationEligibilityRefreshDuration,
            nameof(Unit.SECONDS),
            "Duration of organisation eligibility refreshes"
        );
        _outcome = meter.CreateCounter<long>(
            Metrics.Names.OrganisationEligibilityRefreshOutcome,
            nameof(Unit.COUNT),
            "Count of organisation eligibility refresh outcomes"
        );
        _referenceResolutionCount = meter.CreateHistogram<long>(
            Metrics.Names.OrganisationEligibilityReferenceResolutionCount,
            nameof(Unit.COUNT),
            "Organisation eligibility rows by Account reference resolution state"
        );
        _rowCount = meter.CreateHistogram<long>(
            Metrics.Names.OrganisationEligibilityRefreshRowCount,
            nameof(Unit.COUNT),
            "Rows in an organisation eligibility refresh"
        );
    }

    public void Completed(OrganisationEligibilityRefreshResult result, TimeSpan duration)
    {
        var tags = BuildTags(result.Outcome.ToString());
        _outcome.Add(1, tags);
        _duration.Record(duration.TotalSeconds, tags);
        _rowCount.Record(result.RowCount, tags);
    }

    public void Failed(TimeSpan duration)
    {
        var tags = BuildTags("Failed");
        _outcome.Add(1, tags);
        _duration.Record(duration.TotalSeconds, tags);
    }

    public void LeaseSkipped()
    {
        _outcome.Add(1, BuildTags("LeaseSkipped"));
    }

    public void ReferenceResolutionObserved(IReadOnlyCollection<OrganisationComplianceDeclarationEligibility> rows)
    {
        foreach (var state in Enum.GetValues<OrganisationReferenceNumberResolutionState>())
        {
            var tags = BuildTags();
            tags.Add(Metrics.Tags.ReferenceResolutionState, state.ToString());
            _referenceResolutionCount.Record(rows.Count(x => x.ReferenceNumberResolutionState == state), tags);
        }
    }

    private static TagList BuildTags(string? outcome = null)
    {
        var tags = new TagList { { Metrics.Tags.Service, Process.GetCurrentProcess().ProcessName } };
        if (!string.IsNullOrWhiteSpace(outcome))
            tags.Add(Metrics.Tags.Outcome, outcome);

        return tags;
    }
}
