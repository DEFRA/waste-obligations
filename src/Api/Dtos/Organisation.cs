using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

public record Organisation
{
    [Required]
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [Required]
    [JsonPropertyName("registrationType")]
    public RegistrationType RegistrationType { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

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
