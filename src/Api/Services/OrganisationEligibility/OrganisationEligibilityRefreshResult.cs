namespace Defra.WasteObligations.Api.Services.OrganisationEligibility;

public record OrganisationEligibilityRefreshResult
{
    public required OrganisationEligibilityRefreshOutcome Outcome { get; init; }
    public string? ActiveGeneration { get; init; }
    public required int RowCount { get; init; }
    public required string ContentFingerprint { get; init; }
}
