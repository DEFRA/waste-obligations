using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

public record OrganisationObligations
{
    [JsonPropertyName("organisationId")]
    public required Guid OrganisationId { get; init; }

    [JsonPropertyName("companyName")]
    public string? CompanyName { get; init; }

    [JsonPropertyName("regulator")]
    public string? Regulator { get; init; }

    [JsonPropertyName("obligations")]
    public Obligation[] Obligations { get; init; } = [];
}
