using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

[BsonIgnoreExtraElements]
public record ObligationCalculation
{
    public const string SchemaVersionValue = "v1.0";
    public ObjectId Id { get; init; }
    public int Version { get; init; } = 1;
    public string SchemaVersion { get; private init; } = SchemaVersionValue;

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public required Guid SubmitterOrganisationId { get; init; }

    [BsonRepresentation(BsonType.String)]
    public required ObligationSubmitterType SubmitterType { get; init; }
    public required int ObligationYear { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime CalculatedOn { get; init; }
    public required IReadOnlyList<ProducerObligationCalculation> Producers { get; init; }
}
