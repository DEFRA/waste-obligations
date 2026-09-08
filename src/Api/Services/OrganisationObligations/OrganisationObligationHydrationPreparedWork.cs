namespace Defra.WasteObligations.Api.Services.OrganisationObligations;

public record OrganisationObligationHydrationPreparedWork
{
    public required int ObligationYear { get; init; }

    public required int ActiveSummaryCount { get; init; }

    public required int DueSummaryCount { get; init; }
}
