using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

public record PrnIssuer
{
    [Required]
    [JsonPropertyName("organisationName")]
    public required string OrganisationName { get; init; }
}
