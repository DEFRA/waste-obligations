namespace Defra.WasteObligations.Api.Data.Entities;

public record LocalizedText
{
    public required string Text { get; init; }
    public required string Language { get; init; }
}
