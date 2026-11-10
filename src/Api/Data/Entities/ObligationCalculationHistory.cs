using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

[BsonIgnoreExtraElements]
public record ObligationCalculationHistory
{
    public ObjectId Id { get; init; } = ObjectId.GenerateNewId();
    public required ObligationCalculation Calculation { get; init; }
}
