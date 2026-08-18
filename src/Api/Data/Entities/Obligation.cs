using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

[BsonIgnoreExtraElements]
public record Obligation
{
    public required string Material { get; init; }
    public decimal RecyclingTarget { get; init; }
    public required ObligationTonnages Tonnages { get; init; }
    public required string Status { get; init; }
}
