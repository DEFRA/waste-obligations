using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

[BsonIgnoreExtraElements]
public record OrganisationComplianceDeclarationEligibility
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
    public bool IsVisibleInUnsubmittedView { get; init; }
    public bool? RecyclingObligationsMet { get; init; }
    public decimal ObligationCoveragePercentage { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime DeclarationStateUpdatedAt { get; init; }

    public required string SourceFingerprint { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime RefreshedAt { get; init; }
}
