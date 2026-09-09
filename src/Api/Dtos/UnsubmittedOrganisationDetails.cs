namespace Defra.WasteObligations.Api.Dtos;

public record UnsubmittedOrganisationDetails
{
    public Guid OrganisationId { get; init; }
    public string? ActiveEligibilityGeneration { get; init; }
    public required UnsubmittedOrganisationEligibility[] Eligibility { get; init; }
    public required UnsubmittedOrganisationObligationSummary[] ObligationSummaries { get; init; }
    public UnsubmittedOrganisationLiveData? LiveData { get; init; }
}
