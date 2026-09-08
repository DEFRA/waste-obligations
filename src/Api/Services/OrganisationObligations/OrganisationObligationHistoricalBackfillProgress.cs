namespace Defra.WasteObligations.Api.Services.OrganisationObligations;

public record OrganisationObligationHistoricalBackfillProgress
{
    public required int ProcessedCount { get; init; }
    public required int RemainingCount { get; init; }
    public bool WasEnqueued { get; init; }
}
