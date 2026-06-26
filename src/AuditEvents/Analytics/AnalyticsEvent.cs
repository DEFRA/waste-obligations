using MongoDB.Bson;

namespace Defra.WasteObligations.AuditEvents.Analytics;

public record AnalyticsEvent
{
    public required string EventId { get; init; }
    public long Sequence { get; init; }
    public required string Entity { get; init; }
    public required string EntityId { get; init; }
    public required string Operation { get; init; }
    public DateTime OccurredAt { get; init; }
    public DateTime RecordedAt { get; init; }
    public required string Actor { get; init; }
    public int Version { get; init; }
    public BsonDocument? Before { get; init; }
    public BsonDocument? After { get; init; }
    public required string SchemaVersion { get; init; }
}
