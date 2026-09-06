namespace Defra.WasteObligations.Api.Dtos;

public record OrganisationObligationHydrationPollingStatus
{
    public required bool PollingEnabled { get; init; }
    public required int PollIntervalSeconds { get; init; }
    public required int BatchSize { get; init; }
    public required int MaxConcurrentRequests { get; init; }
    public required int MaxDownstreamRequestsPerMinute { get; init; }
    public required int RefreshIntervalSeconds { get; init; }
    public required int MaximumSummaryStalenessSeconds { get; init; }
    public required OrganisationObligationHydrationYearStatus[] Years { get; init; }
    public required PollingWorkerLeaseStatus Lease { get; init; }
}
