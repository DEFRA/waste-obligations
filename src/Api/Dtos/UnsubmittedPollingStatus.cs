namespace Defra.WasteObligations.Api.Dtos;

public record UnsubmittedPollingStatus
{
    public required OrganisationEligibilityPollingStatus Eligibility { get; init; }
    public required OrganisationObligationHydrationPollingStatus ObligationHydration { get; init; }
}
