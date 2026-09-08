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
    private readonly Histogram<double> _accountReferenceLookupDuration;
    private readonly Counter<long> _accountReferenceLookupFailure;
    private readonly Histogram<long> _accountReferenceLookupBatchSize;
    private readonly Counter<long> _outcome;
    private readonly Histogram<long> _referenceResolutionCount;
    private readonly Histogram<long> _rowCount;
    private readonly Histogram<double> _wasteOrganisationsReadDuration;
    private readonly Counter<long> _wasteOrganisationsReadFailure;
    private readonly Histogram<long> _wasteOrganisationsRowCount;

    public OrganisationEligibilityRefreshMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(Metrics.MeterName);

        _duration = meter.CreateHistogram<double>(
            Metrics.Names.OrganisationEligibilityRefreshDuration,
            nameof(Unit.SECONDS),
            "Duration of organisation eligibility refreshes"
        );
        _accountReferenceLookupDuration = meter.CreateHistogram<double>(
            Metrics.Names.OrganisationEligibilityRefreshAccountReferenceLookupDuration,
            nameof(Unit.SECONDS),
            "Duration of Account reference lookups during an organisation eligibility refresh"
        );
        _accountReferenceLookupFailure = meter.CreateCounter<long>(
            Metrics.Names.OrganisationEligibilityRefreshAccountReferenceLookupFailure,
            nameof(Unit.COUNT),
            "Count of failed Account reference lookups during an organisation eligibility refresh"
        );
        _accountReferenceLookupBatchSize = meter.CreateHistogram<long>(
            Metrics.Names.OrganisationEligibilityRefreshAccountReferenceLookupBatchSize,
            nameof(Unit.COUNT),
            "Lookup keys in an Account reference lookup during an organisation eligibility refresh"
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
        _wasteOrganisationsReadDuration = meter.CreateHistogram<double>(
            Metrics.Names.OrganisationEligibilityRefreshWasteOrganisationsReadDuration,
            nameof(Unit.SECONDS),
            "Duration of Waste Organisations reads during an organisation eligibility refresh"
        );
        _wasteOrganisationsReadFailure = meter.CreateCounter<long>(
            Metrics.Names.OrganisationEligibilityRefreshWasteOrganisationsReadFailure,
            nameof(Unit.COUNT),
            "Count of failed Waste Organisations reads during an organisation eligibility refresh"
        );
        _wasteOrganisationsRowCount = meter.CreateHistogram<long>(
            Metrics.Names.OrganisationEligibilityRefreshWasteOrganisationsRowCount,
            nameof(Unit.COUNT),
            "Organisations returned by Waste Organisations during an organisation eligibility refresh"
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

    public void ReferenceResolutionObserved(IReadOnlyCollection<OrganisationComplianceDeclarationEligibility> rows)
    {
        foreach (var state in Enum.GetValues<OrganisationReferenceNumberResolutionState>())
        {
            var tags = BuildTags();
            tags.Add(Metrics.Tags.ReferenceResolutionState, state.ToString());
            _referenceResolutionCount.Record(rows.Count(x => x.ReferenceNumberResolutionState == state), tags);
        }
    }

    public void WasteOrganisationsReadCompleted(int organisationCount, TimeSpan duration)
    {
        var tags = BuildTags();
        _wasteOrganisationsReadDuration.Record(duration.TotalSeconds, tags);
        _wasteOrganisationsRowCount.Record(organisationCount, tags);
    }

    public void WasteOrganisationsReadFailed(TimeSpan duration)
    {
        var tags = BuildTags();
        _wasteOrganisationsReadFailure.Add(1, tags);
        _wasteOrganisationsReadDuration.Record(duration.TotalSeconds, tags);
    }

    public void AccountReferenceLookupCompleted(
        RegistrationType registrationType,
        int lookupKeyCount,
        TimeSpan duration
    )
    {
        var tags = BuildTags();
        tags.Add(Metrics.Tags.AccountReferenceLookupType, registrationType.ToString());
        _accountReferenceLookupDuration.Record(duration.TotalSeconds, tags);
        _accountReferenceLookupBatchSize.Record(lookupKeyCount, tags);
    }

    public void AccountReferenceLookupFailed(RegistrationType registrationType, TimeSpan duration)
    {
        var tags = BuildTags();
        tags.Add(Metrics.Tags.AccountReferenceLookupType, registrationType.ToString());
        _accountReferenceLookupFailure.Add(1, tags);
        _accountReferenceLookupDuration.Record(duration.TotalSeconds, tags);
    }

    private static TagList BuildTags(string? outcome = null)
    {
        var tags = new TagList { { Metrics.Tags.Service, Process.GetCurrentProcess().ProcessName } };
        if (!string.IsNullOrWhiteSpace(outcome))
            tags.Add(Metrics.Tags.Outcome, outcome);

        return tags;
    }
}
