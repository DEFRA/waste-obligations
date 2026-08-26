namespace Defra.WasteObligations.Api.Services.OrganisationEligibility;

public interface IOrganisationEligibilityRefreshService
{
    Task<OrganisationEligibilityRefreshResult> Refresh(CancellationToken cancellationToken);
}
