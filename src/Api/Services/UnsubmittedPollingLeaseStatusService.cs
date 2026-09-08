using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Dtos;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services;

public class UnsubmittedPollingLeaseStatusService(IMongoDatabase database, TimeProvider timeProvider)
    : IUnsubmittedPollingLeaseStatusService
{
    private readonly IMongoCollection<BackgroundWorkerLease> _leases = database.GetCollection<BackgroundWorkerLease>(
        BackgroundWorkerLease.CollectionName
    );

    public async Task<UnsubmittedPollingLeaseStatus> Get(CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        var leases = await _leases
            .Find(x =>
                x.Id == BackgroundWorkerLease.OrganisationEligibilityRefreshLeaseId
                || x.Id == BackgroundWorkerLease.OrganisationObligationHydrationLeaseId
            )
            .ToListAsync(cancellationToken);

        return new UnsubmittedPollingLeaseStatus
        {
            UtcNow = utcNow,
            Eligibility = Lease(leases, BackgroundWorkerLease.OrganisationEligibilityRefreshLeaseId, utcNow),
            ObligationHydration = Lease(leases, BackgroundWorkerLease.OrganisationObligationHydrationLeaseId, utcNow),
        };
    }

    private static PollingWorkerLeaseStatus Lease(
        IReadOnlyCollection<BackgroundWorkerLease> leases,
        string leaseId,
        DateTime utcNow
    )
    {
        var lease = leases.SingleOrDefault(x => x.Id == leaseId);

        return new PollingWorkerLeaseStatus
        {
            IsHeld = lease is not null && lease.ExpiresAt > utcNow,
            ExpiresAt = lease?.ExpiresAt,
            UpdatedAt = lease?.UpdatedAt,
            LastReleasedAt = lease?.LastReleasedAt,
        };
    }
}
