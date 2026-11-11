using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

[BsonIgnoreExtraElements]
public record MaterialObligationCalculation
{
    public required string MaterialCode { get; init; }
    public required long InputKilograms { get; init; }
    public required long InputTonnes { get; init; }
    public required long ObligationTonnes { get; init; }
}
