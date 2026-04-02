using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

public record OrganisationObligations
{
    [JsonPropertyName("obligations")]
    public Obligation[] Obligations { get; init; } = [];

    [JsonPropertyName("organisation")]
    public Organisation? Organisation { get; init; }
}
