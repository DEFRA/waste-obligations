using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

public record PrnAuthorisedBy
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("position")]
    public string? Position { get; init; }
}
