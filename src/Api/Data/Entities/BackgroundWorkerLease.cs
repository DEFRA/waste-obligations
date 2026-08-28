using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

[BsonIgnoreExtraElements]
public record BackgroundWorkerLease
{
    public const string CollectionName = "_unsubmitted_organisation_worker_leases";
    public const string OrganisationEligibilityRefreshLeaseId = "organisation-eligibility-refresh";
    public const string OrganisationObligationHydrationLeaseId = "organisation-obligation-hydration";

    [BsonId]
    public required string Id { get; init; }

    [BsonElement("owner")]
    public string? Owner { get; init; }

    [BsonElement("expiresAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ExpiresAt { get; init; }

    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; init; }

    [BsonElement("updatedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; init; }

    [BsonElement("lastReleasedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? LastReleasedAt { get; init; }
}
