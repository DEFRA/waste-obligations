namespace Defra.WasteObligations.Api.Dtos;

public record UnsubmittedOrganisationLiveOrganisation
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public string? TradingName { get; init; }
    public string? Country { get; init; }
    public string? CompaniesHouseNumber { get; init; }
    public required Address Address { get; init; }
    public required UnsubmittedOrganisationLiveRegistration[] Registrations { get; init; }
}
