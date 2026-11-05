using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Services.PrnCommonBackend;

public record PrnStatusUpdate
{
    [JsonPropertyName("prnId")]
    public Guid PrnId { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("obligationYear")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ObligationYear { get; init; }

    // When set, the common backend applies the update only to a PRN still in this status.
    [JsonPropertyName("expectedStatus")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExpectedStatus { get; init; }
}
