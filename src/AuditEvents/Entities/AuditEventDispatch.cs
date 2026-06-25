using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.AuditEvents.Entities;

[BsonIgnoreExtraElements]
public record AuditEventDispatch
{
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime SentAt { get; init; }

    public required string SentBy { get; init; }
}
