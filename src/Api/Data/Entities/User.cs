using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

[BsonIgnoreExtraElements]
public record User
{
    public required string Id { get; init; }
    public required string Email { get; init; }
    public required string Name { get; init; }
    public string? Locale { get; init; }
}
