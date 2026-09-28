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

    public string CompanyName(int? registrationYear = null)
    {
        return CompanyName(LatestRegistrationOrByYear(registrationYear));
    }

    public string CompanyName(Registration registration)
    {
        var result = registration.Type switch
        {
            WasteOrganisations.RegistrationType.LargeProducer => Name,
            WasteOrganisations.RegistrationType.ComplianceScheme => TradingName,
            _ => Name,
        };

        // Fall back to the legal name when a compliance scheme has no usable trading
        // name. Blank is treated as missing, not as a name: Waste Organisations accepts
        // and stores an empty tradingName, and a null-only fallback would materialise
        // that as an empty eligibility Name, which the regulator list renders as an
        // empty organisation cell and Mongo sorts ahead of every real name.
        return string.IsNullOrWhiteSpace(result) ? Name : result;
    }

    public string RegistrationType(int? registrationYear = null) => LatestRegistrationOrByYear(registrationYear).Type;

    private int LatestRegistrationYear() => Registrations.MaxBy(x => x.RegistrationYear)?.RegistrationYear ?? 0;

    private Registration LatestRegistrationOrByYear(int? registrationYear)
    {
        registrationYear ??= LatestRegistrationYear();
        var registrations = Registrations
            .Where(x => x.RegistrationYear == registrationYear)
            .OrderByDescending(x => x.Updated)
            .ToArray();

        var registration =
            registrations.FirstOrDefault(x => x.Status == RegistrationStatus.Registered)
            ?? registrations.FirstOrDefault();

        return registration
            ?? throw new InvalidOperationException($"No registration found, using year {registrationYear}");
    }
}
