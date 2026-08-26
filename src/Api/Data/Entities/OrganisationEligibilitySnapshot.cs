using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

[BsonIgnoreExtraElements]
public record OrganisationEligibilitySnapshot
{
    public const string SnapshotId = "unsubmitted-compliance-declarations";

    public required string Id { get; init; } = SnapshotId;
    public string? ActiveGeneration { get; init; }
    public string? ActiveContentFingerprint { get; init; }
    public int ActiveRowCount { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? ActiveGenerationPromotedAt { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? LastVerifiedAt { get; init; }
}
