using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

public record LocalizedText
{
    [Required]
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [Description("Language string for ISO 639-1 (example en) or BCP 47 (example en-GB)")]
    [Required]
    [JsonPropertyName("language")]
    public required string Language { get; init; }
}
