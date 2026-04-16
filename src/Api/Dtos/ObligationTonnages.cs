using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

public record ObligationTonnages
{
    [JsonPropertyName("material")]
    [Range(0, int.MaxValue)]
    public int Material { get; init; }

    [JsonPropertyName("awaitingAcceptance")]
    [Range(0, int.MaxValue)]
    public int AwaitingAcceptance { get; init; }

    [JsonPropertyName("accepted")]
    [Range(0, int.MaxValue)]
    public int Accepted { get; init; }

    [JsonPropertyName("outstanding")]
    [Range(0, int.MaxValue)]
    public int Outstanding { get; init; }

    [JsonPropertyName("obligated")]
    [Range(0, int.MaxValue)]
    public int Obligated { get; init; }
}
