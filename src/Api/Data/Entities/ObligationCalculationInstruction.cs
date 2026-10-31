using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

[BsonIgnoreExtraElements]
public record ObligationCalculationInstruction
{
    public const string CollectionName = "_obligation_calculation_instructions";

    public ObjectId Id { get; init; } = ObjectId.GenerateNewId();

    public required int ObligationYear { get; init; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid? SubmitterId { get; init; }

    [BsonRepresentation(BsonType.String)]
    public ObligationCalculationInstructionStatus Status { get; init; } =
        ObligationCalculationInstructionStatus.Pending;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime CreatedAt { get; init; }
}
