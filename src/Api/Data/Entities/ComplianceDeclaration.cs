using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

public record ComplianceDeclaration
{
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public required Guid Id { get; init; }
    public int Version { get; init; } = 1;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime Created { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime Updated { get; init; }
    public ComplianceDeclarationStatus Status { get; init; }
    public required Organisation Organisation { get; init; }
    public int ObligationYear { get; init; }
    public IEnumerable<Obligation> Obligations { get; init; } = [];
    public required LocalizedText DeclarationText { get; init; }
    public required string SubmitterName { get; init; }
    public required User User { get; init; }
}
