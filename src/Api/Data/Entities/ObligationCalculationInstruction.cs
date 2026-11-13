using System.Text.Json.Serialization;
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

    [JsonIgnore]
    public string? TraceId { get; init; }

    [BsonIgnore]
    [JsonIgnore]
    public string CorrelationId => TraceId ?? Id.ToString();

    [BsonRepresentation(BsonType.String)]
    public ObligationCalculationInstructionStatus Status { get; init; } =
        ObligationCalculationInstructionStatus.Pending;

    public int FailureCount { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime CreatedAt { get; init; }
}
