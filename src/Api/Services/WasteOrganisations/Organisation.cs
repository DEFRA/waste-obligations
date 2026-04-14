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
        var registration = LatestRegistrationOrByYear(year);
        var result = registration.Type switch
        {
            RegistrationType.LargeProducer => Name,
            RegistrationType.ComplianceScheme => TradingName,
            _ => Name,
        };

        return result ?? Name;
    }

    private int LatestRegistrationYear() => Registrations.MaxBy(x => x.RegistrationYear)?.RegistrationYear ?? 0;

    private Registration LatestRegistrationOrByYear(int? year)
    {
        year ??= LatestRegistrationYear();
        var registrations = Registrations.Where(x => x.RegistrationYear == year).ToArray();

        var registration =
            registrations.FirstOrDefault(x => x.Status == RegistrationStatus.Registered)
            ?? registrations.FirstOrDefault();

        return registration ?? throw new InvalidOperationException($"No registration found, using year {year}");
    }
}
