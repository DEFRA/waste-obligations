using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

[BsonIgnoreExtraElements]
public record ObligationCalculation
{
    public const string SchemaVersionValue = "v1.0";
    public ObjectId Id { get; init; }
    public int Version { get; init; } = 1;

    [BsonElement("schemaVersion")]
    public string SchemaVersion { get; private init; } = SchemaVersionValue;

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public required Guid SubmitterOrganisationId { get; init; }

    [BsonRepresentation(BsonType.String)]
    public required ObligationSubmitterType SubmitterType { get; init; }
    public required int ObligationYear { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime CalculatedOn { get; init; }
    public required IReadOnlyList<ProducerObligationCalculation> Producers { get; init; }

    /// <summary>
    /// Determines whether this incoming calculation differs from the current calculation.
    /// Both calculations must belong to the same submitter organisation and obligation year;
    /// those identity fields are not compared. Calculation time, ID, entity version and schema
    /// version are ignored, as is producer and material ordering.
    /// </summary>
    public bool RequiresNewVersion(ObligationCalculation current) =>
        SubmitterType != current.SubmitterType
        || Producers.Count != current.Producers.Count
        || !Producers
            .OrderBy(x => x.ProducerOrganisationId)
            .Zip(current.Producers.OrderBy(x => x.ProducerOrganisationId))
            .All(pair =>
                pair.First.ProducerOrganisationId == pair.Second.ProducerOrganisationId
                && pair.First.Materials.Count == pair.Second.Materials.Count
                && pair.First.Materials.OrderBy(x => x.MaterialCode, StringComparer.Ordinal)
                    .SequenceEqual(pair.Second.Materials.OrderBy(x => x.MaterialCode, StringComparer.Ordinal))
            );
}
