using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Services.AccountBackend;

public record OrganisationWithPersons
{
    [JsonPropertyName("persons")]
    public IReadOnlyList<OrganisationPerson> Persons { get; init; } = [];
}
