using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Dtos;

public record UnsubmittedOrganisationDetails
{
    public Guid OrganisationId { get; init; }
    public OrganisationEligibilitySnapshot? EligibilitySnapshot { get; init; }
    public required OrganisationComplianceDeclarationEligibility[] Eligibility { get; init; }
    public required OrganisationObligationSummary[] ObligationSummaries { get; init; }
    public UnsubmittedOrganisationLiveData? LiveData { get; init; }
}
