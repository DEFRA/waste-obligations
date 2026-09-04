using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services.OrganisationEligibility;

public class OrganisationEligibilityRefreshLeaseService(
    IMongoDatabase database,
    TimeProvider timeProvider,
    ILogger<OrganisationEligibilityRefreshLeaseService> logger
) : IOrganisationEligibilityRefreshLeaseService
{
    private readonly BackgroundWorkerLeaseService _leaseService = new(
        database,
        timeProvider,
        logger,
        BackgroundWorkerLease.CollectionName,
        BackgroundWorkerLease.OrganisationEligibilityRefreshLeaseId,
        "organisation eligibility refresh"
    );

    public Task<bool> TryAcquire(TimeSpan leaseDuration, CancellationToken cancellationToken) =>
        _leaseService.TryAcquire(leaseDuration, cancellationToken);

    public Task<bool> TryRenew(TimeSpan leaseDuration, CancellationToken cancellationToken) =>
        _leaseService.TryRenew(leaseDuration, cancellationToken);

    public Task Release(CancellationToken cancellationToken) => _leaseService.Release(cancellationToken);
}
