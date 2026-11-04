using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

[BsonIgnoreExtraElements]
public record MaterialObligationCalculation
{
    public required string MaterialCode { get; init; }
    public required long InputTonnage { get; init; }
    public required long Obligation { get; init; }
}
