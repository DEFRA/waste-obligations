using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

[BsonIgnoreExtraElements]
public record ComplianceDeclarationReviewState
{
    public ObjectId Id { get; init; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid OrganisationId { get; init; }

    public int ObligationYear { get; init; }
    public RegistrationType RegistrationType { get; init; }
    public int UnsubmittedExclusionCount { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; init; }
}
