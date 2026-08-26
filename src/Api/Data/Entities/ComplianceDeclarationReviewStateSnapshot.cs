using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

[BsonIgnoreExtraElements]
public record ComplianceDeclarationReviewStateSnapshot
{
    public const string SnapshotId = "compliance-declaration-review-state";

    [BsonId]
    public required string Id { get; init; } = SnapshotId;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? BackfillCompletedAt { get; init; }
}
