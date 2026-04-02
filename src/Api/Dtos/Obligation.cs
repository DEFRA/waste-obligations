using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

public record Obligation
{
    [JsonPropertyName("material")]
    public required string Material { get; init; }

    [JsonPropertyName("recyclingTarget")]
    public decimal RecyclingTarget { get; init; }

    [JsonPropertyName("tonnages")]
    public required ObligationTonnages Tonnages { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }
}
