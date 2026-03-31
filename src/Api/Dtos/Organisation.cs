using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

public record Organisation
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }
}
