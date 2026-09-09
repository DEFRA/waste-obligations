namespace Defra.WasteObligations.Api.Dtos;

public record UnsubmittedHistoricalBackfillStart
{
    public required int CurrentObligationYear { get; init; }
    public required UnsubmittedHistoricalBackfillYear[] Years { get; init; }
}
