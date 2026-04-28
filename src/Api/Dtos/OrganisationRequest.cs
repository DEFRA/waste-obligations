using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

public record OrganisationRequest
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

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

    [JsonPropertyName("regulator")]
    public string? Regulator { get; init; }
}
