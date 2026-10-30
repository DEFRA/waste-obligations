using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Services.Scheduling;

internal sealed record ScheduledJobState
{
    [BsonId]
    public required string Id { get; init; }

    [BsonElement("lastStartedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime LastStartedAt { get; init; }
}
