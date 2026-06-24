using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

[BsonIgnoreExtraElements]
public record AuditEvent
{
    [BsonId]
    public required string EventId { get; init; }

    public long Sequence { get; init; }
    public required string Entity { get; init; }
    public required string EntityId { get; init; }
    public required string Operation { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime OccurredAt { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime RecordedAt { get; init; }

    public required string Actor { get; init; }
    public int Version { get; init; }
    public BsonDocument? Before { get; init; }
    public BsonDocument? After { get; init; }
    public required string SchemaVersion { get; init; }
}
