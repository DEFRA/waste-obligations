using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

[BsonIgnoreExtraElements]
public record OrganisationReferenceCache
{
    public ObjectId Id { get; init; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid OrganisationId { get; init; }

    public RegistrationType RegistrationType { get; init; }
    public OrganisationReferenceLookupMode LookupMode { get; init; }
    public string? CompaniesHouseNumber { get; init; }
    public string? ReferenceNumber { get; init; }
    public OrganisationReferenceNumberResolutionState ResolutionState { get; init; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid? ResolvedAccountExternalId { get; init; }

    public string? ResolvedUsingCompaniesHouseNumber { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime FirstSeenAt { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime LastSeenAt { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? LastAttemptedAt { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? NextAttemptAt { get; init; }

    public int AttemptCount { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? ResolvedAt { get; init; }

    public string? LastFailure { get; init; }
}
