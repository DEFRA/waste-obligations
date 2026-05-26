using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

public record OrganisationRequest
{
    [Required]
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [Required]
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("complianceSchemeName")]
    public string? ComplianceSchemeName { get; init; }

    [JsonPropertyName("schemeOperatorName")]
    public string? SchemeOperatorName { get; init; }

    [JsonPropertyName("referenceNumber")]
    public string? ReferenceNumber { get; init; }

    [JsonPropertyName("address")]
    public Address? Address { get; init; }

    [Required]
    [JsonPropertyName("regulator")]
    public required string Regulator { get; init; }

    [Required]
    [JsonPropertyName("regulatorEmail")]
    public required string RegulatorEmail { get; init; }
}
