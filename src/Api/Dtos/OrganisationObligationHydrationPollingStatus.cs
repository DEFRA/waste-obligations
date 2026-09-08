namespace Defra.WasteObligations.Api.Dtos;

public record OrganisationObligationHydrationPollingStatus
{
    public required bool PollingEnabled { get; init; }
    public required int PollIntervalSeconds { get; init; }
    public required int BatchSize { get; init; }
    public required int MaxConcurrentRequests { get; init; }
    public required int MaxDownstreamRequestsPerMinute { get; init; }
    public required double TotalMinimumFullRefreshMinutes { get; init; }
    public required int DesiredRequestsPerMinute { get; init; }
    public required int EffectiveRequestsPerMinute { get; init; }
    public string? RateBackoffReason { get; init; }
    public double? RecentDownstreamReadLatencyMilliseconds { get; init; }
    public required double RecentDownstreamReadFailurePercentage { get; init; }
    public required double EstimatedFullRefreshMinutes { get; init; }
    public required double EstimatedStalenessGapMinutes { get; init; }
    public required int RefreshIntervalSeconds { get; init; }
    public required int MaximumSummaryStalenessSeconds { get; init; }
    public required OrganisationObligationHistoricalBackfillPollingStatus[] HistoricalBackfills { get; init; }
    public required OrganisationObligationHydrationYearStatus[] Years { get; init; }
    public required PollingWorkerLeaseStatus Lease { get; init; }
}
