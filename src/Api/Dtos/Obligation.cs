using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Defra.WasteObligations.Api.Dtos.Attributes;

namespace Defra.WasteObligations.Api.Dtos;

public record Obligation
{
    [Description("Name of material")]
    [Required]
    [PossibleValue(Dtos.Material.Plastic)]
    [PossibleValue(Dtos.Material.Glass)]
    [PossibleValue(Dtos.Material.Aluminium)]
    [PossibleValue(Dtos.Material.Steel)]
    [PossibleValue(Dtos.Material.Wood)]
    [PossibleValue(Dtos.Material.GlassRemelt)]
    [PossibleValue(Dtos.Material.Paper)]
    [JsonPropertyName("material")]
    public required string Material { get; init; }

    [Description("Recycling target expressed in decimal form")]
    [Range(typeof(decimal), "0", "1")]
    [JsonPropertyName("recyclingTarget")]
    public decimal RecyclingTarget { get; init; }

    [Required]
    [JsonPropertyName("tonnages")]
    public required ObligationTonnages Tonnages { get; init; }

    [Description("Status of obligation")]
    [Required]
    [PossibleValue(ObligationStatus.NoDataYet)]
    [PossibleValue(ObligationStatus.Met)]
    [PossibleValue(ObligationStatus.NotMet)]
    [JsonPropertyName("status")]
    public required string Status { get; init; }
}
