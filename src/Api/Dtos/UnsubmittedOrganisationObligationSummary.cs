namespace Defra.WasteObligations.Api.Dtos;

public record UnsubmittedOrganisationObligationSummary
{
    public int ObligationYear { get; init; }
    public int ObligationCount { get; init; }
    public int TotalAcceptedTonnage { get; init; }
    public int TotalObligatedTonnage { get; init; }
    public bool? RecyclingObligationsMet { get; init; }
    public decimal? ObligationCoveragePercentage { get; init; }
    public string? SourceFingerprint { get; init; }
    public DateTime? LastSuccessfulReadAt { get; init; }
    public string? DailyCalculationRunId { get; init; }
    public DateTime LastAttemptedAt { get; init; }
    public DateTime NextRefreshAt { get; init; }
    public required string Priority { get; init; }
    public DateTime RequestedAt { get; init; }
    public bool IsHydrationActive { get; init; }
    public required string RefreshState { get; init; }
    public int AttemptCount { get; init; }
    public string? LastFailure { get; init; }
}
