using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

public record ComplianceDeclaration
{
    public required ObjectId Id { get; init; }
    public int Version { get; init; } = 1;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime Created { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime Updated { get; init; }
    public ComplianceDeclarationStatus Status { get; init; }
    public required Organisation Organisation { get; init; }
    public int ObligationYear { get; init; }
    public IEnumerable<Obligation> Obligations { get; init; } = [];
    public required string ObligationStatus { get; init; }
    public required LocalizedText DeclarationText { get; init; }
    public required string SubmitterName { get; init; }
    public IEnumerable<AuditEntry> Audit { get; init; } = [];
}
