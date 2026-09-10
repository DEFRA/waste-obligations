using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services.OrganisationObligations;

public class OrganisationObligationHistoricalBackfillLeaseService(IMongoDatabase database, TimeProvider timeProvider)
    : IOrganisationObligationHistoricalBackfillLeaseService
{
    private readonly BackgroundWorkerLeaseService _leaseService = new(
        database,
        timeProvider,
        BackgroundWorkerLease.CollectionName,
        BackgroundWorkerLease.OrganisationObligationHydrationLeaseId
    );

    public Task<bool> TryAcquire(TimeSpan leaseDuration, CancellationToken cancellationToken) =>
        _leaseService.TryAcquire(leaseDuration, cancellationToken);

    public Task<bool> TryRenew(TimeSpan leaseDuration, CancellationToken cancellationToken) =>
        _leaseService.TryRenew(leaseDuration, cancellationToken);

    public Task Release(CancellationToken cancellationToken) => _leaseService.Release(cancellationToken);
}
