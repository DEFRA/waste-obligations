namespace Defra.WasteObligations.Api.Services.WasteOrganisations;

public interface IOrganisationEligibilitySource
{
    Task<OrganisationSearch> Search(CancellationToken cancellationToken);
}
