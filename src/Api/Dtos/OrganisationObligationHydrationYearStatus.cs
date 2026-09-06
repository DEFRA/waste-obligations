namespace Defra.WasteObligations.Api.Dtos;

public record OrganisationObligationHydrationYearStatus
{
    public required int ObligationYear { get; init; }
    public required int ActiveSummaryCount { get; init; }
    public required int DueSummaryCount { get; init; }
    public required int PendingSummaryCount { get; init; }
    public required int ReadySummaryCount { get; init; }
    public required int FailedSummaryCount { get; init; }
    public required int NeverSuccessfullyReadSummaryCount { get; init; }
    public DateTime? OldestDueAt { get; init; }
    public DateTime? OldestSuccessfulReadAt { get; init; }
    public DateTime? LatestSuccessfulReadAt { get; init; }
    public required double MinimumFullRefreshMinutes { get; init; }
}
