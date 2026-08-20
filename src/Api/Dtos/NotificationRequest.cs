using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

public record NotificationRequest
{
    [JsonPropertyName("parameters")]
    public IReadOnlyDictionary<string, string>? Parameters { get; init; }
}
