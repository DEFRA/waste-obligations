using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

public record UpdatePrnRequest
{
    [Required]
    [JsonPropertyName("status")]
    public UpdatePrnStatus? Status { get; init; }

    [Required]
    [JsonPropertyName("user")]
    public required User User { get; init; }
}
