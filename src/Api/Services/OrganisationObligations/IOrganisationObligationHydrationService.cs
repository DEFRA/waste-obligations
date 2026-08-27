namespace Defra.WasteObligations.Api.Services.OrganisationObligations;

public interface IOrganisationObligationHydrationService
{
    Task<int> EnqueueNewEligible(int obligationYear, CancellationToken cancellationToken);
    Task<int> HydrateDue(int obligationYear, CancellationToken cancellationToken);
}
