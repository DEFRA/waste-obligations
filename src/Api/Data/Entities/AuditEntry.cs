using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

[BsonKnownTypes(typeof(ReasonAuditEntry))]
public record AuditEntry(string Action)
{
    public required User User { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime Timestamp { get; init; }
}

public record ReasonAuditEntry(string Action) : AuditEntry(Action)
{
    public required string Reason { get; init; }
}
