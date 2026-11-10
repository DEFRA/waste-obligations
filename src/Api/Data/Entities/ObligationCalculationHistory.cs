using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

[BsonIgnoreExtraElements]
public record ObligationCalculationHistory
{
    public ObjectId Id { get; init; } = ObjectId.GenerateNewId();
    public required ObjectId CalculationId { get; init; }
    public required int Version { get; init; }
    public required BsonDocument Calculation { get; init; }
}
