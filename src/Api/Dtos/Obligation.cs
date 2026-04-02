using System.Text.Json.Serialization;
using Defra.WasteObligations.Api.Dtos.Attributes;

namespace Defra.WasteObligations.Api.Dtos;

public record Obligation
{
    [JsonPropertyName("material")]
    public required string Material { get; init; }

    [JsonPropertyName("recyclingTarget")]
    public decimal RecyclingTarget { get; init; }

    [JsonPropertyName("tonnages")]
    public required ObligationTonnages Tonnages { get; init; }

    [PossibleValue("NoDataYet")]
    [PossibleValue("Met")]
    [PossibleValue("NotMet")]
    [JsonPropertyName("status")]
    public required string Status { get; init; }
}
