using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

[BsonIgnoreExtraElements]
public record BackgroundWorkerLease
{
    public const string OrganisationEligibilityRefreshCollectionName = "_organisation_eligibility_refresh_lease";
    public const string OrganisationEligibilityRefreshLeaseId = "organisation-eligibility-refresh";
    public const string OrganisationObligationHydrationCollectionName = "_organisation_obligation_hydration_lease";
    public const string OrganisationObligationHydrationLeaseId = "organisation-obligation-hydration";

    [BsonId]
    public required string Id { get; init; }

    public string? Owner { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ExpiresAt { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? LastReleasedAt { get; init; }
}
