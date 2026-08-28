namespace Defra.WasteObligations.Api.Services.OrganisationEligibility;

public interface IOrganisationEligibilityRefreshLeaseService
{
    Task<bool> TryAcquire(TimeSpan leaseDuration, CancellationToken cancellationToken);
    Task<bool> TryRenew(TimeSpan leaseDuration, CancellationToken cancellationToken);
    Task Release(CancellationToken cancellationToken);
}
