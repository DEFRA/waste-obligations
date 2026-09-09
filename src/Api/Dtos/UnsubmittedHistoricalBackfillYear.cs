namespace Defra.WasteObligations.Api.Dtos;

public record UnsubmittedHistoricalBackfillYear
{
    public required int ObligationYear { get; init; }
    public required int PotentialHydrationOrganisationCount { get; init; }
    public required string Status { get; init; }
}
