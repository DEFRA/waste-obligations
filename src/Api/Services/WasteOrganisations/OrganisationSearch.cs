using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Services.WasteOrganisations;

public record OrganisationSearch
{
    [JsonPropertyName("organisations")]
    public Organisation[] Organisations { get; init; } = [];
}
