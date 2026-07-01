using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.AuditEvents.Entities;

public record AuditEventDispatch
{
    public AuditEventDispatchStatus Status { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime Date { get; init; }

    public string? Message { get; init; }

    public int AttemptCount { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    [BsonIgnoreIfNull]
    public DateTime? NextAttemptAt { get; init; }
}
