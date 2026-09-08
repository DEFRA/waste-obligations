using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services.OrganisationEligibility;
using Defra.WasteObligations.Api.Services.OrganisationObligations;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

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
        var activeRowSummary = await ActiveRowSummary(snapshot?.ActiveGeneration, cancellationToken);
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
                activeRowSummary,
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

    private async Task<OrganisationEligibilityPollingSummary> ActiveRowSummary(
        string? activeGeneration,
        CancellationToken cancellationToken
    )
    {
        if (activeGeneration is null)
        {
            return new OrganisationEligibilityPollingSummary
            {
                VisibleRowCount = 0,
                HydrationEligibleOrganisationCount = 0,
                ReferenceResolutionStateCounts = new Dictionary<OrganisationReferenceNumberResolutionState, int>(),
            };
        }

        var activeRows = dbContext
            .OrganisationComplianceDeclarationEligibilities.AsQueryable()
            .Where(x => x.Generation == activeGeneration);
        var visibleRowCountTask = activeRows.Where(x => x.IsVisibleInUnsubmittedView).CountAsync(cancellationToken);
        var hydrationEligibleOrganisationCountTask = activeRows
            .Where(x =>
                x.RegistrationStatus == OrganisationRegistrationStatus.Registered
                && x.ReferenceNumberResolutionState == OrganisationReferenceNumberResolutionState.Resolved
            )
            .Select(x => new { x.OrganisationId, x.ObligationYear })
            .Distinct()
            .CountAsync(cancellationToken);
        var referenceResolutionStateCountsTask = activeRows
            .GroupBy(x => x.ReferenceNumberResolutionState)
            .Select(x => new { State = x.Key, Count = x.Count() })
            .ToListAsync(cancellationToken);
        await Task.WhenAll(
            visibleRowCountTask,
            hydrationEligibleOrganisationCountTask,
            referenceResolutionStateCountsTask
        );

        return new OrganisationEligibilityPollingSummary
        {
            VisibleRowCount = await visibleRowCountTask,
            HydrationEligibleOrganisationCount = await hydrationEligibleOrganisationCountTask,
            ReferenceResolutionStateCounts = (await referenceResolutionStateCountsTask).ToDictionary(
                x => x.State,
                x => x.Count
            ),
        };
    }

    private static OrganisationEligibilityPollingStatus EligibilityStatus(
        OrganisationEligibilitySnapshot? snapshot,
        OrganisationEligibilityPollingSummary activeRowSummary,
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
            VisibleRowCount = activeRowSummary.VisibleRowCount,
            HydrationEligibleOrganisationCount = activeRowSummary.HydrationEligibleOrganisationCount,
            ReferenceResolutionStates =
            [
                .. Enum.GetValues<OrganisationReferenceNumberResolutionState>()
                    .Select(state => new OrganisationReferenceResolutionStatus
                    {
                        State = state.ToString(),
                        Count = activeRowSummary.ReferenceResolutionStateCounts.GetValueOrDefault(state),
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
            TotalMinimumFullRefreshMinutes = summaries.Count / (double)options.MaxDownstreamRequestsPerMinute,
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
