using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Services.AccountBackend;

public record AccountOrganisation
{
    [JsonPropertyName("externalId")]
    public string? ExternalId { get; init; }

    [JsonPropertyName("referenceNumber")]
    public string? ReferenceNumber { get; init; }

    [JsonPropertyName("companiesHouseNumber")]
    public string? CompaniesHouseNumber { get; init; }

    [JsonPropertyName("isComplianceScheme")]
    public bool IsComplianceScheme { get; init; }
}
