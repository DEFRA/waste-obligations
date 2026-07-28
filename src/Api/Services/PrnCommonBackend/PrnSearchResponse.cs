using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Services.PrnCommonBackend;

public record PrnSearchResponse
{
    [JsonPropertyName("items")]
    public IEnumerable<PrnData> Items { get; init; } = [];

    [JsonPropertyName("totalItems")]
    public int TotalItems { get; init; }
}
