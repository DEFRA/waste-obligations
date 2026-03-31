using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

public record Obligation
{
    [JsonPropertyName("materialName")]
    public required string MaterialName { get; init; }

    [JsonPropertyName("recyclingTarget")]
    public decimal RecyclingTarget { get; init; }

    [JsonPropertyName("tonnage")]
    public int Tonnage { get; init; }

    [JsonPropertyName("obligatedTonnage")]
    public int? ObligatedTonnage { get; init; }

    [JsonPropertyName("tonnageAwaitingAcceptance")]
    public int TonnageAwaitingAcceptance { get; init; }

    [JsonPropertyName("acceptedTonnage")]
    public int AcceptedTonnage { get; init; }

    [JsonPropertyName("outstandingTonnage")]
    public int? OutstandingTonnage { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }
}
