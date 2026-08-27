using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Services.AccountBackend;

public record OrganisationsByExternalIdsResponse
{
    [JsonPropertyName("organisations")]
    public IReadOnlyList<AccountOrganisation> Organisations { get; init; } = [];

    [JsonPropertyName("notFoundExternalIds")]
    public IReadOnlyList<string> NotFoundExternalIds { get; init; } = [];
}
