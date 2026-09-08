using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services.OrganisationEligibility;
using Defra.WasteObligations.Api.Services.OrganisationObligations;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services;

public class UnsubmittedPollingStatusService(
    IDbContext dbContext,
    IMongoDatabase database,
    IOptions<OrganisationEligibilityOptions> eligibilityOptions,
    IOptions<OrganisationObligationHydrationOptions> obligationHydrationOptions,
    TimeProvider timeProvider
) : IUnsubmittedPollingStatusService
{
    public async Task<UnsubmittedPollingStatus> Get(CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        var snapshot = await dbContext
            .OrganisationEligibilitySnapshots.Find(x => x.Id == OrganisationEligibilitySnapshot.SnapshotId)
            .SingleOrDefaultAsync(cancellationToken);
        var activeRows = await ActiveRows(snapshot?.ActiveGeneration, cancellationToken);
        var activeSummaries = await dbContext
            .OrganisationObligationSummaries.Find(x => x.IsHydrationActive)
            .ToListAsync(cancellationToken);
        var leases = await database
            .GetCollection<BackgroundWorkerLease>(BackgroundWorkerLease.CollectionName)
            .Find(x =>
                x.Id == BackgroundWorkerLease.OrganisationEligibilityRefreshLeaseId
                || x.Id == BackgroundWorkerLease.OrganisationObligationHydrationLeaseId
            )
            .ToListAsync(cancellationToken);

        return new UnsubmittedPollingStatus
        {
            Eligibility = EligibilityStatus(
                snapshot,
                activeRows,
                eligibilityOptions.Value,
                Lease(leases, BackgroundWorkerLease.OrganisationEligibilityRefreshLeaseId, utcNow)
            ),
            ObligationHydration = ObligationHydrationStatus(
                activeSummaries,
                obligationHydrationOptions.Value,
                Lease(leases, BackgroundWorkerLease.OrganisationObligationHydrationLeaseId, utcNow),
                utcNow
            ),
        };
    }

    private async Task<IReadOnlyList<OrganisationComplianceDeclarationEligibility>> ActiveRows(
        string? activeGeneration,
        CancellationToken cancellationToken
    )
    {
        if (activeGeneration is null)
            return [];

        return await dbContext
            .OrganisationComplianceDeclarationEligibilities.Find(x => x.Generation == activeGeneration)
            .ToListAsync(cancellationToken);
    }

    private static OrganisationEligibilityPollingStatus EligibilityStatus(
        OrganisationEligibilitySnapshot? snapshot,
        IReadOnlyList<OrganisationComplianceDeclarationEligibility> rows,
        OrganisationEligibilityOptions options,
        PollingWorkerLeaseStatus lease
    ) =>
        new()
        {
            ActiveGeneration = snapshot?.ActiveGeneration,
            ActiveContentFingerprint = snapshot?.ActiveContentFingerprint,
            ActiveRowCount = snapshot?.ActiveRowCount ?? 0,
            ActiveGenerationPromotedAt = snapshot?.ActiveGenerationPromotedAt,
            LastVerifiedAt = snapshot?.LastVerifiedAt,
            MaterialisedStateVersion = snapshot?.MaterialisedStateVersion ?? 0,
            RefreshPollingEnabled = options.RefreshPollingEnabled,
            RefreshPollIntervalSeconds = options.RefreshPollIntervalSeconds,
            VisibleRowCount = rows.Count(x => x.IsVisibleInUnsubmittedView),
            HydrationEligibleOrganisationCount = rows.Where(x =>
                    x.RegistrationStatus == OrganisationRegistrationStatus.Registered
                    && x.ReferenceNumberResolutionState == OrganisationReferenceNumberResolutionState.Resolved
                )
                .Select(x => (x.OrganisationId, x.ObligationYear))
                .Distinct()
                .Count(),
            ReferenceResolutionStates =
            [
                .. Enum.GetValues<OrganisationReferenceNumberResolutionState>()
                    .Select(state => new OrganisationReferenceResolutionStatus
                    {
                        State = state.ToString(),
                        Count = rows.Count(x => x.ReferenceNumberResolutionState == state),
                    }),
            ],
            Lease = lease,
        };

    private static OrganisationObligationHydrationPollingStatus ObligationHydrationStatus(
        IReadOnlyList<OrganisationObligationSummary> summaries,
        OrganisationObligationHydrationOptions options,
        PollingWorkerLeaseStatus lease,
        DateTime utcNow
    ) =>
        new()
        {
            PollingEnabled = options.PollingEnabled,
            PollIntervalSeconds = options.PollIntervalSeconds,
            BatchSize = options.BatchSize,
            MaxConcurrentRequests = options.MaxConcurrentRequests,
            MaxDownstreamRequestsPerMinute = options.MaxDownstreamRequestsPerMinute,
            RefreshIntervalSeconds = (int)options.RefreshInterval.TotalSeconds,
            MaximumSummaryStalenessSeconds = (int)options.MaximumSummaryStaleness.TotalSeconds,
            Years =
            [
                .. summaries
                    .GroupBy(x => x.ObligationYear)
                    .OrderBy(x => x.Key)
                    .Select(group => HydrationYearStatus(group.Key, group.ToArray(), options, utcNow)),
            ],
            Lease = lease,
        };

    private static OrganisationObligationHydrationYearStatus HydrationYearStatus(
        int obligationYear,
        OrganisationObligationSummary[] summaries,
        OrganisationObligationHydrationOptions options,
        DateTime utcNow
    )
    {
        var dueSummaries = summaries.Where(x => x.NextRefreshAt <= utcNow).ToArray();
        var successfulReadTimes = summaries
            .Where(x => x.LastSuccessfulReadAt is not null)
            .Select(x => x.LastSuccessfulReadAt!.Value)
            .ToArray();

        return new OrganisationObligationHydrationYearStatus
        {
            ObligationYear = obligationYear,
            ActiveSummaryCount = summaries.Length,
            DueSummaryCount = dueSummaries.Length,
            PendingSummaryCount = summaries.Count(x => x.RefreshState == OrganisationObligationRefreshState.Pending),
            ReadySummaryCount = summaries.Count(x => x.RefreshState == OrganisationObligationRefreshState.Ready),
            FailedSummaryCount = summaries.Count(x => x.RefreshState == OrganisationObligationRefreshState.Failed),
            NeverSuccessfullyReadSummaryCount = summaries.Count(x => x.LastSuccessfulReadAt is null),
            OldestDueAt = dueSummaries.Length == 0 ? null : dueSummaries.Min(x => x.NextRefreshAt),
            OldestSuccessfulReadAt = successfulReadTimes.Length == 0 ? null : successfulReadTimes.Min(),
            LatestSuccessfulReadAt = successfulReadTimes.Length == 0 ? null : successfulReadTimes.Max(),
            MinimumFullRefreshMinutes = summaries.Length / (double)options.MaxDownstreamRequestsPerMinute,
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
