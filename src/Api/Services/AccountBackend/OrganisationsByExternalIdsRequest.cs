using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Services.AccountBackend;

public record OrganisationsByExternalIdsRequest
{
    [JsonPropertyName("externalIds")]
    public IReadOnlyCollection<Guid> ExternalIds { get; init; } = [];
}
