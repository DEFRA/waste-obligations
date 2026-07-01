using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Defra.WasteObligations.AuditEvents.Analytics;

public record AnalyticsEvent
{
    [JsonPropertyName("eventId")]
    public required string EventId { get; init; }

    [JsonPropertyName("sequence")]
    public long Sequence { get; init; }

    [JsonPropertyName("entity")]
    public required string Entity { get; init; }

    [JsonPropertyName("entityId")]
    public required string EntityId { get; init; }

    [JsonPropertyName("operation")]
    public required string Operation { get; init; }

    [Description("ISO 8601 extended format with offset")]
    [JsonPropertyName("occurredAt")]
    public DateTimeOffset OccurredAt { get; init; }

    [Description("ISO 8601 extended format with offset")]
    [JsonPropertyName("recordedAt")]
    public DateTimeOffset RecordedAt { get; init; }

    [JsonPropertyName("actor")]
    public required string Actor { get; init; }

    [JsonPropertyName("version")]
    public int Version { get; init; }

    [JsonPropertyName("before")]
    public object? Before { get; init; }

    [JsonPropertyName("after")]
    public object? After { get; init; }

    [JsonPropertyName("schemaVersion")]
    public required string SchemaVersion { get; init; }
}
