namespace Defra.WasteObligations.Api.Dtos;

public record OrganisationObligationHistoricalBackfillPollingStatus
{
    public required int ObligationYear { get; init; }
    public required int PotentialHydrationOrganisationCount { get; init; }
    public required string Status { get; init; }
    public required DateTime RequestedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? DeferralReason { get; init; }
    public DateTime? DeferredAt { get; init; }
}
