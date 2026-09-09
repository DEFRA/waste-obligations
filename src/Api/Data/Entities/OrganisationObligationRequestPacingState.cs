using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

[BsonIgnoreExtraElements]
public record OrganisationObligationRequestPacingState
{
    public const string CollectionName = "_unsubmitted_organisation_obligation_request_pacing";
    public const string StateId = "organisation-obligation-hydration";

    [BsonId]
    public required string Id { get; init; }

    public required int DesiredRequestsPerMinute { get; init; }

    public required int EffectiveRequestsPerMinute { get; init; }

    public string? BackoffReason { get; init; }

    public double? RecentDownstreamLatencyMilliseconds { get; init; }

    public required double RecentDownstreamFailurePercentage { get; init; }

    public double? BaselineDownstreamLatencyMilliseconds { get; init; }

    public required double RateAdjustment { get; init; }

    public required bool IsUnderPressure { get; init; }

    public OrganisationObligationRequestPacingRead[] RecentReads { get; init; } = [];

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? NextRequestAt { get; init; }

    public required long Version { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime UpdatedAt { get; init; }
}
