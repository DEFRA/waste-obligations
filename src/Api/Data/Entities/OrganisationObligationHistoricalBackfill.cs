using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

[BsonIgnoreExtraElements]
public record OrganisationObligationHistoricalBackfill
{
    public const string CollectionName = "_unsubmitted_organisation_historical_backfills";

    [BsonId]
    public required int ObligationYear { get; init; }

    public OrganisationObligationHistoricalBackfillTarget[] Targets { get; init; } = [];

    [BsonIgnore]
    [JsonIgnore]
    public Guid[] OrganisationIds
    {
        get => [.. Targets.Select(x => x.OrganisationId)];
        init =>
            Targets = [
                .. value.Select(organisationId => new OrganisationObligationHistoricalBackfillTarget
                {
                    OrganisationId = organisationId,
                }),
            ];
    }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime RequestedAt { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime UpdatedAt { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? EnqueuedAt { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? CompletedAt { get; init; }

    [BsonRepresentation(BsonType.String)]
    public OrganisationObligationHistoricalBackfillDeferralReason? DeferralReason { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? DeferredAt { get; init; }
}
