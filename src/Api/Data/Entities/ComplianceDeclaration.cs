using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

public record ComplianceDeclaration
{
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public required Guid Id { get; init; }
    public int Version { get; init; } = 1;
    public DateTime Created { get; init; }
    public DateTime Updated { get; init; }
}
