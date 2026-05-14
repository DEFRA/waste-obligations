using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

[BsonKnownTypes(typeof(SubmissionAuditEntry))]
[BsonKnownTypes(typeof(CancelledAuditEntry))]
public abstract record AuditEntry(string Action)
{
    public required User User { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime Timestamp { get; init; }
}

public record SubmissionAuditEntry() : AuditEntry("Submitted");

public record CancelledAuditEntry() : AuditEntry("Cancelled")
{
    public required string Reason { get; init; }
}
