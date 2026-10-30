using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services.Scheduling;

public sealed class ScheduledJobLeaseService(IMongoDatabase database, TimeProvider timeProvider, string jobName)
    : IScheduledJobLeaseService
{
    private readonly BackgroundWorkerLeaseService _lease = new(
        database,
        timeProvider,
        "_scheduled_job_leases",
        jobName
    );

    public Task<bool> TryAcquire(TimeSpan leaseDuration, CancellationToken cancellationToken) =>
        _lease.TryAcquire(leaseDuration, cancellationToken);

    public Task<bool> TryRenew(TimeSpan leaseDuration, CancellationToken cancellationToken) =>
        _lease.TryRenew(leaseDuration, cancellationToken);

    public Task Release(CancellationToken cancellationToken) => _lease.Release(cancellationToken);
}
