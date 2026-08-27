using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Services.AccountBackend;

public record OrganisationsByCompaniesHouseNumbersRequest
{
    [JsonPropertyName("companiesHouseNumbers")]
    public IReadOnlyCollection<string> CompaniesHouseNumbers { get; init; } = [];
}
