using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

[JsonDerivedType(typeof(CancelledAuditEntry))]
public record AuditEntry
{
    [JsonPropertyName("user")]
    public required User User { get; init; }

    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("action")]
    public required string Action { get; init; }
}

public record CancelledAuditEntry : AuditEntry
{
    [JsonPropertyName("reason")]
    public required string Reason { get; init; }
}
