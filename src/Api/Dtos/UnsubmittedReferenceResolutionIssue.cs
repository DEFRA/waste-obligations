namespace Defra.WasteObligations.Api.Dtos;

public record UnsubmittedReferenceResolutionIssue
{
    public required Guid OrganisationId { get; init; }

    public required int ObligationYear { get; init; }

    public required RegistrationType RegistrationType { get; init; }

    public required string RegistrationStatus { get; init; }

    public required string Name { get; init; }

    public string? TradingName { get; init; }

    public string? CompaniesHouseNumber { get; init; }

    public string? Country { get; init; }

    public required string ReferenceResolutionState { get; init; }
}
