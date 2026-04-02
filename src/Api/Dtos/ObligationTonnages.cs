using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

public record ObligationTonnages
{
    [JsonPropertyName("material")]
    public int Material { get; init; }

    [JsonPropertyName("awaitingAcceptance")]
    public int AwaitingAcceptance { get; init; }

    [JsonPropertyName("accepted")]
    public int Accepted { get; init; }

    [JsonPropertyName("outstanding")]
    public int Outstanding { get; init; }

    [JsonPropertyName("obligated")]
    public int Obligated { get; init; }
}
