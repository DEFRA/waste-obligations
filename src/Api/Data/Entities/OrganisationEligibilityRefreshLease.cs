using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

[BsonIgnoreExtraElements]
public record OrganisationEligibilityRefreshLease
{
    public const string CollectionName = "_organisation_eligibility_refresh_lease";
    public const string LeaseId = "organisation-eligibility-refresh";

    [BsonId]
    public required string Id { get; init; } = LeaseId;

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
