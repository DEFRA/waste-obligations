using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

public record RetainedOrganisationEligibilityGeneration
{
    public required string Generation { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime DeleteAfter { get; init; }
}
