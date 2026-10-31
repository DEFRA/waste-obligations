namespace Defra.WasteObligations.Api.Services.Scheduling;

public interface IScheduledJobLeaseService
{
    Task<bool> TryAcquire(TimeSpan leaseDuration, CancellationToken cancellationToken);

    Task<bool> TryRenew(TimeSpan leaseDuration, CancellationToken cancellationToken);

    Task Release(CancellationToken cancellationToken);
}
