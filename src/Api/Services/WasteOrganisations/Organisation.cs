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

    public string CompanyName(int? year = null)
    {
        var registration = year is not null
            ? Registrations.FirstOrDefault(x => x.RegistrationYear == year)
            : Registrations.OrderByDescending(x => x.RegistrationYear).FirstOrDefault();

        if (registration is null)
            throw new InvalidOperationException(
                $"No registration found{(year is not null ? $" for year \"{year}\"" : "")}"
            );

        if (registration.Status == RegistrationStatus.Cancelled)
            throw new InvalidOperationException("Registration is cancelled");

        var result = registration.Type switch
        {
            RegistrationType.LargeProducer => Name,
            RegistrationType.ComplianceScheme => TradingName,
            _ => Name,
        };

        return result ?? Name;
    }
}
