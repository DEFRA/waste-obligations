using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Services.AccountBackend;

public record OrganisationsByExternalIdsRequest
{
    [JsonPropertyName("externalIds")]
    public IReadOnlyCollection<Guid> ExternalIds { get; init; } = [];
}

public record OrganisationsByExternalIdsResponse
{
    [JsonPropertyName("organisations")]
    public IReadOnlyList<AccountOrganisation> Organisations { get; init; } = [];

    [JsonPropertyName("notFoundExternalIds")]
    public IReadOnlyList<string> NotFoundExternalIds { get; init; } = [];
}

public record OrganisationsByCompaniesHouseNumbersRequest
{
    [JsonPropertyName("companiesHouseNumbers")]
    public IReadOnlyCollection<string> CompaniesHouseNumbers { get; init; } = [];
}

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
