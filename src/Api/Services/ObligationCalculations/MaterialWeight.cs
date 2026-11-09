namespace Defra.WasteObligations.Api.Services.ObligationCalculations;

public record MaterialWeight
{
    public required string MaterialCode { get; init; }
    public required long Kilograms { get; init; }
}
