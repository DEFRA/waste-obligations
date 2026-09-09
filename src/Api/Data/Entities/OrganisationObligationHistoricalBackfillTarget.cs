using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

public record OrganisationObligationHistoricalBackfillTarget
{
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public required Guid OrganisationId { get; init; }
}
