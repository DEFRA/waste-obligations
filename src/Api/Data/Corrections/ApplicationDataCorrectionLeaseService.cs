using Defra.WasteObligations.Api.Services;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Data.Corrections;

public class ApplicationDataCorrectionLeaseService(IMongoDatabase database, TimeProvider timeProvider)
    : IApplicationDataCorrectionLeaseService
{
    private readonly BackgroundWorkerLeaseService _lease = new(
        database,
        timeProvider,
        "_application_data_correction_lease",
        "application-data-corrections"
    );

    public Task<bool> TryAcquire(TimeSpan duration, CancellationToken cancellationToken) =>
        _lease.TryAcquire(duration, cancellationToken);

    public Task<bool> TryRenew(TimeSpan duration, CancellationToken cancellationToken) =>
        _lease.TryRenew(duration, cancellationToken);

    public Task Release(CancellationToken cancellationToken) => _lease.Release(cancellationToken);
}
