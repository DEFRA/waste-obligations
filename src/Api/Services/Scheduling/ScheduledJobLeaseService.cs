using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services.Scheduling;

public sealed class ScheduledJobLeaseService(
    IMongoDatabase database,
    TimeProvider timeProvider,
    string jobName,
    string? instructionId = null
) : IScheduledJobLeaseService
{
    private readonly BackgroundWorkerLeaseService _lease = new(
        database,
        timeProvider,
        instructionId is null ? "_scheduled_job_leases" : "_scheduled_job_processing_leases",
        instructionId is null ? jobName : $"{jobName}:{instructionId}"
    );

    public Task<bool> TryAcquire(TimeSpan leaseDuration, CancellationToken cancellationToken) =>
        _lease.TryAcquire(leaseDuration, cancellationToken);

    public Task<bool> TryRenew(TimeSpan leaseDuration, CancellationToken cancellationToken) =>
        _lease.TryRenew(leaseDuration, cancellationToken);

    public Task Release(CancellationToken cancellationToken) => _lease.Release(cancellationToken);
}
