using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteObligations.Api.Data.Entities;

public record Organisation
{
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public string? ComplianceSchemeName { get; init; }
    public string? SchemeOperatorName { get; init; }
    public string? ReferenceNumber { get; init; }
    public Address? Address { get; init; }
    public string? Regulator { get; init; }
}
