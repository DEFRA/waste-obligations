using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

[BsonIgnoreExtraElements]
public record ProducerObligationCalculation
{
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public required Guid ProducerOrganisationId { get; init; }
    public required IReadOnlyList<MaterialObligationCalculation> Materials { get; init; }
}
