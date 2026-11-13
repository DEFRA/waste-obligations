namespace Defra.WasteObligations.Api.Services.WastePackaging;

public record MaterialWeight
{
    public required string MaterialCode { get; init; }
    public required long Kilograms { get; init; }
}
