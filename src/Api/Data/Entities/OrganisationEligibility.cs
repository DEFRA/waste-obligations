using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

[BsonIgnoreExtraElements]
public record OrganisationEligibility
{
    public ObjectId Id { get; init; } = ObjectId.GenerateNewId();
    public required string Generation { get; init; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid OrganisationId { get; init; }

    public int ObligationYear { get; init; }
    public RegistrationType RegistrationType { get; init; }
    public OrganisationRegistrationStatus RegistrationStatus { get; init; }
    public required string Name { get; init; }
    public string? TradingName { get; init; }
    public string? CompaniesHouseNumber { get; init; }
    public string? ReferenceNumber { get; init; }
    public OrganisationReferenceNumberResolutionState ReferenceNumberResolutionState { get; init; }
    public required string SourceFingerprint { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime RefreshedAt { get; init; }
}
