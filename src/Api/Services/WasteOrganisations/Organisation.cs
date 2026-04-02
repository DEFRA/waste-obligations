using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Services.WasteOrganisations;

public record Organisation
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("tradingName")]
    public string? TradingName { get; init; }

    [JsonPropertyName("businessCountry")]
    public string? BusinessCountry { get; init; }

    [JsonPropertyName("companiesHouseNumber")]
    public string? CompaniesHouseNumber { get; init; }

    [JsonPropertyName("address")]
    public required Address Address { get; init; }

    [JsonPropertyName("registrations")]
    public Registration[] Registrations { get; init; } = [];
}
