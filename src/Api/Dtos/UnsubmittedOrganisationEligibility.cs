namespace Defra.WasteObligations.Api.Dtos;

public record UnsubmittedOrganisationEligibility
{
    public required string Generation { get; init; }
    public int ObligationYear { get; init; }
    public RegistrationType RegistrationType { get; init; }
    public required string RegistrationStatus { get; init; }
    public string? Country { get; init; }
    public required string Name { get; init; }
    public string? TradingName { get; init; }
    public string? CompaniesHouseNumber { get; init; }
    public string? ReferenceNumber { get; init; }
    public required string ReferenceResolutionState { get; init; }
    public bool IsVisibleInUnsubmittedView { get; init; }
    public bool? RecyclingObligationsMet { get; init; }
    public decimal? ObligationCoveragePercentage { get; init; }
    public DateTime DeclarationStateUpdatedAt { get; init; }
    public required string SourceFingerprint { get; init; }
    public DateTime RefreshedAt { get; init; }
}
