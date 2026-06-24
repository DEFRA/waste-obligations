using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

[BsonIgnoreExtraElements]
public record AuditEventCounter
{
    [BsonId]
    public required string Id { get; init; }

    public long Sequence { get; init; }
}
