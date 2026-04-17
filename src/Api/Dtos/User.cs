using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

public record User
{
    [Required]
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [Required]
    [JsonPropertyName("email")]
    public required string Email { get; init; }
}
