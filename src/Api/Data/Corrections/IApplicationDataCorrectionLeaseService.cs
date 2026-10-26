namespace Defra.WasteObligations.Api.Data.Corrections;

public interface IApplicationDataCorrectionLeaseService
{
    Task<bool> TryAcquire(TimeSpan duration, CancellationToken cancellationToken);
    Task<bool> TryRenew(TimeSpan duration, CancellationToken cancellationToken);
    Task Release(CancellationToken cancellationToken);
}
