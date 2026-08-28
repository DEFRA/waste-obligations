using System.ComponentModel.DataAnnotations;

namespace Defra.WasteObligations.Api.Services.OrganisationEligibility;

public record OrganisationEligibilityOptions
{
    public const string SectionName = "OrganisationEligibility";

    public bool RefreshPollingEnabled { get; init; } = true;

    [Range(1, 86400)]
    public int RefreshPollIntervalSeconds { get; init; } = 1800;

    [Range(2, 3600)]
    public int RefreshLeaseDurationSeconds { get; init; } = 300;

    [Range(1, 300)]
    public int RefreshLeaseRenewalIntervalSeconds { get; init; } = 60;

    // TEMPORARY INITIAL ROLLOUT: Remove with the delayed review-state reconciliation worker.
    [Range(1, 3600)]
    public int InitialRolloutReconciliationDelaySeconds { get; init; } = 300;

    public TimeSpan MaximumAllowedStaleness { get; init; } = TimeSpan.FromHours(2);

    [Range(1, 1000)]
    public int AccountReferenceNumberBatchSize { get; init; } = 100;

    public TimeSpan GenerationRetentionPeriod { get; init; } = TimeSpan.FromDays(30);
}
