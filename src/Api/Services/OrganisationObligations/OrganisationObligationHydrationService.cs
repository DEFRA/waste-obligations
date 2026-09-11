using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Api.Utils.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using PrnObligation = Defra.WasteObligations.Api.Services.PrnCommonBackend.Obligation;

namespace Defra.WasteObligations.Api.Services.OrganisationObligations;

public class OrganisationObligationHydrationService(
    IDbContext dbContext,
    IOrganisationObligationSource obligationSource,
    IOrganisationObligationRequestPacer requestPacer,
    IOrganisationObligationHydrationMetrics metrics,
    IOptions<OrganisationObligationHydrationOptions> options,
    TimeProvider timeProvider,
    ILogger<OrganisationObligationHydrationService> logger
) : IOrganisationObligationHydrationService
{
    private const int MaximumFailureLength = 1000;

    public async Task<int> EnqueueNewEligible(int obligationYear, CancellationToken cancellationToken)
    {
        var eligibility = await GetEligibleOrganisationIds(obligationYear, cancellationToken);
        if (!eligibility.HasActiveGeneration)
            return 0;

        var result = await EnqueueNewEligible(eligibility.OrganisationIds, obligationYear, cancellationToken);

        return result;
    }

    public async Task<int> HydrateDue(int obligationYear, CancellationToken cancellationToken, int? maximumWork = null)
    {
        var work = await PrepareDueWork(obligationYear, cancellationToken);
        await requestPacer.ObserveWorkload(work.ActiveSummaryCount, cancellationToken);

        return await HydratePreparedDueWork(work, cancellationToken, maximumWork);
    }

    public async Task<int> EnqueueReconciliation(
        int obligationYear,
        DateTime reconciliationSince,
        CancellationToken cancellationToken
    )
    {
        var eligibility = await GetEligibleOrganisationIds(obligationYear, cancellationToken);
        if (!eligibility.HasActiveGeneration)
            return 0;

        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        var result = await dbContext.OrganisationObligationSummaries.UpdateManyAsync(
            x =>
                x.ObligationYear == obligationYear
                && eligibility.OrganisationIds.Contains(x.OrganisationId)
                && x.IsHydrationActive
                && x.Priority == OrganisationObligationHydrationPriority.ScheduledRefresh
                && (x.LastSuccessfulReadAt == null || x.LastSuccessfulReadAt < reconciliationSince),
            Builders<OrganisationObligationSummary>
                .Update.Set(x => x.Priority, OrganisationObligationHydrationPriority.Reconciliation)
                .Set(x => x.NextRefreshAt, utcNow),
            cancellationToken: cancellationToken
        );

        return (int)result.ModifiedCount;
    }

    public async Task<OrganisationObligationHydrationPreparedWork> PrepareDueWork(
        int obligationYear,
        CancellationToken cancellationToken
    )
    {
        var eligibility = await GetEligibleOrganisationIds(obligationYear, cancellationToken);
        if (!eligibility.HasActiveGeneration)
        {
            return new OrganisationObligationHydrationPreparedWork
            {
                ObligationYear = obligationYear,
                ActiveSummaryCount = 0,
                DueSummaryCount = 0,
            };
        }

        await RemoveInactiveWork(eligibility.OrganisationIds, obligationYear, cancellationToken);
        await EnqueueNewEligible(eligibility.OrganisationIds, obligationYear, cancellationToken);
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        var activeSummaryCountTask = dbContext.OrganisationObligationSummaries.CountDocumentsAsync(
            x => x.ObligationYear == obligationYear && x.IsHydrationActive,
            cancellationToken: cancellationToken
        );
        var dueSummaryCountTask = dbContext.OrganisationObligationSummaries.CountDocumentsAsync(
            x => x.ObligationYear == obligationYear && x.IsHydrationActive && x.NextRefreshAt <= utcNow,
            cancellationToken: cancellationToken
        );
        await Task.WhenAll(activeSummaryCountTask, dueSummaryCountTask);

        return new OrganisationObligationHydrationPreparedWork
        {
            ObligationYear = obligationYear,
            ActiveSummaryCount = (int)await activeSummaryCountTask,
            DueSummaryCount = (int)await dueSummaryCountTask,
        };
    }

    public async Task<int> HydratePreparedDueWork(
        OrganisationObligationHydrationPreparedWork work,
        CancellationToken cancellationToken,
        int? maximumWork = null,
        bool deactivateAfterSuccessfulRead = false,
        bool recordWorkloadMetrics = true
    )
    {
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        if (recordWorkloadMetrics)
        {
            var pacing = await requestPacer.GetStatus(cancellationToken);
            metrics.QueueObserved(work.ActiveSummaryCount, work.DueSummaryCount);
            metrics.CapacityObserved(
                work.ActiveSummaryCount,
                options.Value.MaxDownstreamRequestsPerMinute,
                pacing.DesiredRequestsPerMinute,
                pacing.EffectiveRequestsPerMinute,
                options.Value.RefreshInterval
            );
        }

        if (work.ActiveSummaryCount == 0)
            return 0;

        var dueWork = await dbContext
            .OrganisationObligationSummaries.Find(x =>
                x.ObligationYear == work.ObligationYear && x.IsHydrationActive && x.NextRefreshAt <= utcNow
            )
            .SortBy(x => x.Priority)
            .ThenBy(x => x.NextRefreshAt)
            .Limit(maximumWork ?? options.Value.BatchSize)
            .ToListAsync(cancellationToken);
        var processedCount = 0;
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = options.Value.MaxConcurrentRequests,
        };

        await Parallel.ForEachAsync(
            dueWork,
            parallelOptions,
            async (item, token) =>
            {
                await Hydrate(item, deactivateAfterSuccessfulRead, token);
                Interlocked.Increment(ref processedCount);
            }
        );
        await ObserveStaleness(work.ObligationYear, utcNow, cancellationToken);

        return processedCount;
    }

    public async Task<OrganisationObligationHistoricalBackfillProgress> HydrateHistoricalBackfill(
        OrganisationObligationHistoricalBackfill backfill,
        CancellationToken cancellationToken,
        int? maximumWork = null,
        bool preserveCurrentYearPacing = false
    )
    {
        if (!preserveCurrentYearPacing)
        {
            await requestPacer.ObserveWorkload(backfill.OrganisationIds.Length, cancellationToken);
        }

        if (backfill.OrganisationIds.Length == 0)
        {
            return new OrganisationObligationHistoricalBackfillProgress { ProcessedCount = 0, RemainingCount = 0 };
        }

        var wasEnqueued = false;
        if (backfill.EnqueuedAt is null)
        {
            await EnqueueHistoricalBackfillWork(backfill, cancellationToken);
            wasEnqueued = true;
        }

        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        var work = await dbContext
            .OrganisationObligationSummaries.Find(HistoricalBackfillWorkFilter(backfill, utcNow))
            .SortBy(x => x.Priority)
            .ThenBy(x => x.NextRefreshAt)
            .Limit(maximumWork ?? options.Value.BatchSize)
            .ToListAsync(cancellationToken);
        var processedCount = 0;
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = options.Value.MaxConcurrentRequests,
        };

        await Parallel.ForEachAsync(
            work,
            parallelOptions,
            async (item, token) =>
            {
                await Hydrate(item, deactivateAfterSuccessfulRead: true, token);
                Interlocked.Increment(ref processedCount);
            }
        );
        var remainingCount = await dbContext.OrganisationObligationSummaries.CountDocumentsAsync(
            HistoricalBackfillOutstandingWorkFilter(backfill),
            cancellationToken: cancellationToken
        );
        if (remainingCount == 0)
        {
            await dbContext.OrganisationObligationSummaries.UpdateManyAsync(
                HistoricalBackfillFilter(backfill),
                Builders<OrganisationObligationSummary>.Update.Set(x => x.IsHydrationActive, false),
                cancellationToken: cancellationToken
            );
        }

        return new OrganisationObligationHistoricalBackfillProgress
        {
            ProcessedCount = processedCount,
            RemainingCount = (int)remainingCount,
            WasEnqueued = wasEnqueued,
        };
    }

    private async Task<(bool HasActiveGeneration, Guid[] OrganisationIds)> GetEligibleOrganisationIds(
        int obligationYear,
        CancellationToken cancellationToken
    )
    {
        var snapshot = await dbContext
            .OrganisationEligibilitySnapshots.Find(x => x.Id == OrganisationEligibilitySnapshot.SnapshotId)
            .SingleOrDefaultAsync(cancellationToken);
        if (snapshot?.ActiveGeneration is null)
            return (false, []);

        var eligibleOrganisationIds = await dbContext
            .OrganisationComplianceDeclarationEligibilities.Find(x =>
                x.Generation == snapshot.ActiveGeneration
                && x.ObligationYear == obligationYear
                && x.RegistrationStatus == OrganisationRegistrationStatus.Registered
                && x.ReferenceNumberResolutionState == OrganisationReferenceNumberResolutionState.Resolved
            )
            .Project(x => x.OrganisationId)
            .ToListAsync(cancellationToken);

        return (true, eligibleOrganisationIds.Distinct().ToArray());
    }

    private async Task<int> EnqueueNewEligible(
        Guid[] organisationIds,
        int obligationYear,
        CancellationToken cancellationToken
    )
    {
        if (organisationIds.Length == 0)
            return 0;

        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        await ReactivateExistingEligible(organisationIds, obligationYear, utcNow, cancellationToken);
        var work = organisationIds
            .Select(organisationId => new UpdateOneModel<OrganisationObligationSummary>(
                Builders<OrganisationObligationSummary>.Filter.And(
                    Builders<OrganisationObligationSummary>.Filter.Eq(x => x.OrganisationId, organisationId),
                    Builders<OrganisationObligationSummary>.Filter.Eq(x => x.ObligationYear, obligationYear)
                ),
                Builders<OrganisationObligationSummary>
                    .Update.SetOnInsert(x => x.OrganisationId, organisationId)
                    .SetOnInsert(x => x.ObligationYear, obligationYear)
                    .SetOnInsert(x => x.Priority, OrganisationObligationHydrationPriority.NewEligible)
                    .SetOnInsert(x => x.NextRefreshAt, utcNow)
                    .SetOnInsert(x => x.AttemptCount, 0)
                    .SetOnInsert(x => x.RequestedAt, utcNow)
                    .SetOnInsert(x => x.RefreshState, OrganisationObligationRefreshState.Pending)
                    .Set(x => x.IsHydrationActive, true)
            )
            {
                IsUpsert = true,
            })
            .ToArray();

        var result = await dbContext.OrganisationObligationSummaries.BulkWriteAsync(
            work,
            cancellationToken: cancellationToken
        );

        return result.Upserts.Count;
    }

    private async Task EnqueueHistoricalBackfillWork(
        OrganisationObligationHistoricalBackfill backfill,
        CancellationToken cancellationToken
    )
    {
        var work = backfill
            .OrganisationIds.Select(organisationId => new UpdateOneModel<OrganisationObligationSummary>(
                Builders<OrganisationObligationSummary>.Filter.And(
                    Builders<OrganisationObligationSummary>.Filter.Eq(x => x.OrganisationId, organisationId),
                    Builders<OrganisationObligationSummary>.Filter.Eq(x => x.ObligationYear, backfill.ObligationYear)
                ),
                Builders<OrganisationObligationSummary>.Update.Combine(
                    HistoricalBackfillUpdate(backfill),
                    Builders<OrganisationObligationSummary>.Update.SetOnInsert(x => x.OrganisationId, organisationId),
                    Builders<OrganisationObligationSummary>.Update.SetOnInsert(
                        x => x.ObligationYear,
                        backfill.ObligationYear
                    )
                )
            )
            {
                IsUpsert = true,
            })
            .ToArray();

        await dbContext.OrganisationObligationSummaries.BulkWriteAsync(work, cancellationToken: cancellationToken);
    }

    private async Task ReactivateExistingEligible(
        Guid[] organisationIds,
        int obligationYear,
        DateTime utcNow,
        CancellationToken cancellationToken
    )
    {
        var result = await dbContext.OrganisationObligationSummaries.UpdateManyAsync(
            x =>
                x.ObligationYear == obligationYear
                && !x.IsHydrationActive
                && organisationIds.Contains(x.OrganisationId),
            Builders<OrganisationObligationSummary>
                .Update.Set(x => x.NextRefreshAt, utcNow)
                .Set(x => x.Priority, OrganisationObligationHydrationPriority.NewEligible)
                .Set(x => x.RequestedAt, utcNow)
                .Set(x => x.IsHydrationActive, true)
                .Set(x => x.RefreshState, OrganisationObligationRefreshState.Pending),
            cancellationToken: cancellationToken
        );
        if (result.ModifiedCount > 0)
        {
            logger.LogInformation(
                "Reactivated {ReactivatedSummaryCount} organisation obligation hydration summaries for obligation year {ObligationYear}",
                result.ModifiedCount,
                obligationYear
            );
        }
    }

    private async Task RemoveInactiveWork(
        Guid[] organisationIds,
        int obligationYear,
        CancellationToken cancellationToken
    )
    {
        var filter = Builders<OrganisationObligationSummary>.Filter.And(
            Builders<OrganisationObligationSummary>.Filter.Eq(x => x.ObligationYear, obligationYear),
            Builders<OrganisationObligationSummary>.Filter.Eq(x => x.IsHydrationActive, true)
        );
        if (organisationIds.Length > 0)
        {
            filter &= Builders<OrganisationObligationSummary>.Filter.Nin(x => x.OrganisationId, organisationIds);
        }

        await dbContext.OrganisationObligationSummaries.UpdateManyAsync(
            filter,
            Builders<OrganisationObligationSummary>.Update.Set(x => x.IsHydrationActive, false),
            cancellationToken: cancellationToken
        );
    }

    private async Task Hydrate(
        OrganisationObligationSummary work,
        bool deactivateAfterSuccessfulRead,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await requestPacer.Wait(cancellationToken);
            var obligations = await ReadObligations(work, cancellationToken);
            var summaryMetrics = OrganisationObligationSummaryMapper.Map(
                work.OrganisationId,
                work.ObligationYear,
                obligations
            );
            var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
            var nextRefreshAt = deactivateAfterSuccessfulRead
                ? utcNow
                : NextRefreshAt(work.OrganisationId, work.ObligationYear, utcNow);
            var summary = work with
            {
                ObligationCount = summaryMetrics.ObligationCount,
                TotalAcceptedTonnage = summaryMetrics.TotalAcceptedTonnage,
                TotalObligatedTonnage = summaryMetrics.TotalObligatedTonnage,
                RecyclingObligationsMet = summaryMetrics.RecyclingObligationsMet,
                ObligationCoveragePercentage = summaryMetrics.ObligationCoveragePercentage,
                SourceFingerprint = summaryMetrics.SourceFingerprint,
                LastSuccessfulReadAt = utcNow,
                LastAttemptedAt = utcNow,
                NextRefreshAt = nextRefreshAt,
                RefreshState = OrganisationObligationRefreshState.Ready,
                AttemptCount = 0,
                LastFailure = null,
                Priority = OrganisationObligationHydrationPriority.ScheduledRefresh,
                IsHydrationActive = !deactivateAfterSuccessfulRead,
            };

            await Persist(summary, cancellationToken);
            metrics.Succeeded();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            await RecordFailure(work, exception, cancellationToken);
            metrics.Failed();
        }
    }

    private async Task<IEnumerable<PrnObligation>> ReadObligations(
        OrganisationObligationSummary work,
        CancellationToken cancellationToken
    )
    {
        var readStopwatch = Stopwatch.StartNew();

        try
        {
            var obligations = await obligationSource.ReadObligations(
                work.OrganisationId,
                work.ObligationYear,
                cancellationToken
            );
            readStopwatch.Stop();
            await ObserveRead(readStopwatch.Elapsed, succeeded: true, cancellationToken);
            metrics.ObligationReadCompleted(readStopwatch.Elapsed);

            return obligations;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            readStopwatch.Stop();
            await ObserveRead(readStopwatch.Elapsed, succeeded: false, cancellationToken);
            metrics.ObligationReadFailed(readStopwatch.Elapsed);
            throw;
        }
    }

    private async Task ObserveRead(TimeSpan duration, bool succeeded, CancellationToken cancellationToken)
    {
        try
        {
            await requestPacer.ObserveRead(duration, succeeded, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to persist organisation obligation hydration pacing state");
        }
    }

    private async Task Persist(OrganisationObligationSummary summary, CancellationToken cancellationToken)
    {
        await dbContext.ExecuteTransaction(
            async (session, token) =>
            {
                await dbContext.OrganisationObligationSummaries.ReplaceOneAsync(
                    session,
                    x => x.OrganisationId == summary.OrganisationId && x.ObligationYear == summary.ObligationYear,
                    summary,
                    new ReplaceOptions { IsUpsert = true },
                    token
                );
                var activeGeneration = await dbContext
                    .OrganisationEligibilitySnapshots.Find(
                        session,
                        x => x.Id == OrganisationEligibilitySnapshot.SnapshotId
                    )
                    .Project(x => x.ActiveGeneration)
                    .SingleOrDefaultAsync(token);
                if (activeGeneration is not null)
                {
                    var result = await dbContext.OrganisationComplianceDeclarationEligibilities.UpdateManyAsync(
                        session,
                        x =>
                            x.Generation == activeGeneration
                            && x.OrganisationId == summary.OrganisationId
                            && x.ObligationYear == summary.ObligationYear,
                        Builders<OrganisationComplianceDeclarationEligibility>
                            .Update.Set(x => x.RecyclingObligationsMet, summary.RecyclingObligationsMet)
                            .Set(x => x.ObligationCoveragePercentage, summary.ObligationCoveragePercentage),
                        cancellationToken: token
                    );

                    if (result.ModifiedCount > 0)
                    {
                        await OrganisationEligibilitySnapshotState.IncrementMaterialisedStateVersion(
                            dbContext,
                            session,
                            token
                        );
                    }
                }

                return true;
            },
            "persist organisation obligation hydration result",
            cancellationToken
        );
    }

    private async Task RecordFailure(
        OrganisationObligationSummary work,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        var attemptCount = work.AttemptCount + 1;
        var nextAttemptAt = utcNow.Add(RetryDelay(attemptCount));
        var failure =
            exception.Message.Length > MaximumFailureLength
                ? exception.Message[..MaximumFailureLength]
                : exception.Message;
        var summary = work with
        {
            LastAttemptedAt = utcNow,
            NextRefreshAt = nextAttemptAt,
            RefreshState = OrganisationObligationRefreshState.Failed,
            AttemptCount = attemptCount,
            LastFailure = failure,
            Priority = OrganisationObligationHydrationPriority.Retry,
            IsHydrationActive = true,
        };

        await Persist(summary, cancellationToken);
    }

    private TimeSpan RetryDelay(int attemptCount)
    {
        var multiplier = 1L << Math.Min(attemptCount - 1, 20);
        var maximumRetryDelay = options.Value.MaximumRetryDelay;
        var initialRetryDelay = options.Value.InitialRetryDelay;
        var retryTicks =
            initialRetryDelay.Ticks > maximumRetryDelay.Ticks / multiplier
                ? maximumRetryDelay.Ticks
                : initialRetryDelay.Ticks * multiplier;

        return TimeSpan.FromTicks(retryTicks);
    }

    private async Task ObserveStaleness(int obligationYear, DateTime utcNow, CancellationToken cancellationToken)
    {
        var staleBefore = utcNow.Subtract(options.Value.MaximumSummaryStaleness);
        var staleSummaryTimes = await dbContext
            .OrganisationObligationSummaries.Find(x =>
                x.ObligationYear == obligationYear
                && x.IsHydrationActive
                && (
                    (x.LastSuccessfulReadAt != null && x.LastSuccessfulReadAt < staleBefore)
                    || (x.LastSuccessfulReadAt == null && x.RequestedAt < staleBefore)
                )
            )
            .Project(x => x.LastSuccessfulReadAt ?? x.RequestedAt)
            .ToListAsync(cancellationToken);
        var oldestStaleSummaryAgeSeconds =
            staleSummaryTimes.Count > 0 ? (utcNow - staleSummaryTimes.Min()).TotalSeconds : 0;

        metrics.StalenessObserved(staleSummaryTimes.Count, oldestStaleSummaryAgeSeconds);
        if (staleSummaryTimes.Count > 0)
        {
            logger.LogError(
                "Organisation obligation hydration has {StaleSummaryCount} active summaries older than {MaximumSummaryStaleness}. The oldest is {OldestStaleSummaryAgeSeconds} seconds old for obligation year {ObligationYear}",
                staleSummaryTimes.Count,
                options.Value.MaximumSummaryStaleness,
                oldestStaleSummaryAgeSeconds,
                obligationYear
            );
        }
    }

    private DateTime NextRefreshAt(Guid organisationId, int obligationYear, DateTime utcNow)
    {
        var intervalTicks = options.Value.RefreshInterval.Ticks;
        var currentIntervalStart = utcNow.Ticks - utcNow.Ticks % intervalTicks;
        var fingerprint = SHA256.HashData(Encoding.UTF8.GetBytes($"{organisationId:N}|{obligationYear}"));
        var slot = BitConverter.ToUInt64(fingerprint) % (ulong)intervalTicks;
        var nextRefreshAt = new DateTime(currentIntervalStart + (long)slot, DateTimeKind.Utc);

        return nextRefreshAt <= utcNow ? nextRefreshAt.AddTicks(intervalTicks) : nextRefreshAt;
    }

    private static UpdateDefinition<OrganisationObligationSummary> HistoricalBackfillUpdate(
        OrganisationObligationHistoricalBackfill backfill
    ) =>
        Builders<OrganisationObligationSummary>
            .Update.Set(x => x.Priority, OrganisationObligationHydrationPriority.NewEligible)
            .Set(x => x.NextRefreshAt, backfill.RequestedAt)
            .Set(x => x.AttemptCount, 0)
            .Set(x => x.RequestedAt, backfill.RequestedAt)
            .Set(x => x.RefreshState, OrganisationObligationRefreshState.Pending)
            .Set(x => x.LastFailure, null)
            .Set(x => x.IsHydrationActive, true);

    private static FilterDefinition<OrganisationObligationSummary> HistoricalBackfillFilter(
        OrganisationObligationHistoricalBackfill backfill
    ) =>
        Builders<OrganisationObligationSummary>.Filter.And(
            Builders<OrganisationObligationSummary>.Filter.Eq(x => x.ObligationYear, backfill.ObligationYear),
            Builders<OrganisationObligationSummary>.Filter.Eq(x => x.RequestedAt, backfill.RequestedAt)
        );

    private static FilterDefinition<OrganisationObligationSummary> HistoricalBackfillWorkFilter(
        OrganisationObligationHistoricalBackfill backfill,
        DateTime utcNow
    ) =>
        Builders<OrganisationObligationSummary>.Filter.And(
            HistoricalBackfillOutstandingWorkFilter(backfill),
            Builders<OrganisationObligationSummary>.Filter.Eq(x => x.IsHydrationActive, true),
            Builders<OrganisationObligationSummary>.Filter.Lte(x => x.NextRefreshAt, utcNow)
        );

    private static FilterDefinition<OrganisationObligationSummary> HistoricalBackfillOutstandingWorkFilter(
        OrganisationObligationHistoricalBackfill backfill
    ) =>
        Builders<OrganisationObligationSummary>.Filter.And(
            HistoricalBackfillFilter(backfill),
            Builders<OrganisationObligationSummary>.Filter.Or(
                Builders<OrganisationObligationSummary>.Filter.Eq(x => x.LastSuccessfulReadAt, null),
                Builders<OrganisationObligationSummary>.Filter.Lt(x => x.LastSuccessfulReadAt, backfill.RequestedAt)
            )
        );
}
