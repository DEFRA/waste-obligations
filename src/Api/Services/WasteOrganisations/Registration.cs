using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Services.WasteOrganisations;

public record Registration
{
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("registrationYear")]
    public required int RegistrationYear { get; init; }
}
