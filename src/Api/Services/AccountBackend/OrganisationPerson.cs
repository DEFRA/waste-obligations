using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Services.AccountBackend;

public record OrganisationPerson
{
    [JsonPropertyName("userId")]
    public Guid? UserId { get; init; }

    [JsonPropertyName("firstName")]
    public string? FirstName { get; init; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("serviceRole")]
    public string? ServiceRole { get; init; }
}
