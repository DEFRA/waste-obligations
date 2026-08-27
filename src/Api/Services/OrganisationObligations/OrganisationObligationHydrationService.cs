using System.Security.Cryptography;
using System.Text;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services.OrganisationObligations;

public class OrganisationObligationHydrationService(
    IDbContext dbContext,
    IOrganisationObligationSource obligationSource,
    IOptions<OrganisationObligationHydrationOptions> options,
    TimeProvider timeProvider
) : IOrganisationObligationHydrationService
{
    private const int MaximumFailureLength = 1000;

    public async Task<int> EnqueueNewEligible(int obligationYear, CancellationToken cancellationToken)
    {
        var organisationIds = await GetEligibleOrganisationIds(obligationYear, cancellationToken);
        var result = await EnqueueNewEligible(organisationIds, obligationYear, cancellationToken);

        return result;
    }

    public async Task<int> HydrateDue(int obligationYear, CancellationToken cancellationToken)
    {
        var organisationIds = await GetEligibleOrganisationIds(obligationYear, cancellationToken);
        await RemoveInactiveWork(organisationIds, obligationYear, cancellationToken);
        await EnqueueNewEligible(organisationIds, obligationYear, cancellationToken);
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        var work = await dbContext
            .OrganisationObligationHydrationWork.Find(x =>
                x.ObligationYear == obligationYear && x.NextAttemptAt <= utcNow
            )
            .SortBy(x => x.Priority)
            .ThenBy(x => x.NextAttemptAt)
            .Limit(options.Value.BatchSize)
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
                await Hydrate(item, token);
                Interlocked.Increment(ref processedCount);
            }
        );

        return processedCount;
    }

    private async Task<Guid[]> GetEligibleOrganisationIds(int obligationYear, CancellationToken cancellationToken)
    {
        var snapshot = await dbContext
            .OrganisationEligibilitySnapshots.Find(x => x.Id == OrganisationEligibilitySnapshot.SnapshotId)
            .SingleOrDefaultAsync(cancellationToken);
        if (snapshot?.ActiveGeneration is null)
            return [];

        var eligibleOrganisationIds = await dbContext
            .OrganisationComplianceDeclarationEligibilities.Find(x =>
                x.Generation == snapshot.ActiveGeneration
                && x.ObligationYear == obligationYear
                && x.RegistrationStatus == OrganisationRegistrationStatus.Registered
                && x.ReferenceNumberResolutionState == OrganisationReferenceNumberResolutionState.Resolved
            )
            .Project(x => x.OrganisationId)
            .ToListAsync(cancellationToken);

        return eligibleOrganisationIds.Distinct().ToArray();
    }

    private async Task<int> EnqueueNewEligible(
        Guid[] organisationIds,
        int obligationYear,
        CancellationToken cancellationToken
    )
    {
        if (organisationIds.Length == 0)
            return 0;

        var summaries = await dbContext
            .OrganisationObligationSummaries.Find(x =>
                x.ObligationYear == obligationYear && organisationIds.Contains(x.OrganisationId)
            )
            .ToListAsync(cancellationToken);
        var successfulSummaryOrganisationIds = summaries
            .Where(x => x.LastSuccessfulReadAt is not null)
            .Select(x => x.OrganisationId)
            .ToHashSet();
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        var work = organisationIds
            .Where(x => !successfulSummaryOrganisationIds.Contains(x))
            .Select(organisationId => new UpdateOneModel<OrganisationObligationHydrationWork>(
                Builders<OrganisationObligationHydrationWork>.Filter.And(
                    Builders<OrganisationObligationHydrationWork>.Filter.Eq(x => x.OrganisationId, organisationId),
                    Builders<OrganisationObligationHydrationWork>.Filter.Eq(x => x.ObligationYear, obligationYear)
                ),
                Builders<OrganisationObligationHydrationWork>
                    .Update.SetOnInsert(x => x.OrganisationId, organisationId)
                    .SetOnInsert(x => x.ObligationYear, obligationYear)
                    .SetOnInsert(x => x.Priority, OrganisationObligationHydrationPriority.NewEligible)
                    .SetOnInsert(x => x.NextAttemptAt, utcNow)
                    .SetOnInsert(x => x.AttemptCount, 0)
                    .SetOnInsert(x => x.RequestedAt, utcNow)
            )
            {
                IsUpsert = true,
            })
            .ToArray();
        if (work.Length == 0)
            return 0;

        var result = await dbContext.OrganisationObligationHydrationWork.BulkWriteAsync(
            work,
            cancellationToken: cancellationToken
        );

        return result.Upserts.Count;
    }

    private async Task RemoveInactiveWork(
        Guid[] organisationIds,
        int obligationYear,
        CancellationToken cancellationToken
    )
    {
        var filter = Builders<OrganisationObligationHydrationWork>.Filter.Eq(x => x.ObligationYear, obligationYear);
        if (organisationIds.Length > 0)
        {
            filter &= Builders<OrganisationObligationHydrationWork>.Filter.Nin(x => x.OrganisationId, organisationIds);
        }

        await dbContext.OrganisationObligationHydrationWork.DeleteManyAsync(filter, cancellationToken);
    }

    private async Task Hydrate(OrganisationObligationHydrationWork work, CancellationToken cancellationToken)
    {
        try
        {
            var obligations = await obligationSource.ReadObligations(
                work.OrganisationId,
                work.ObligationYear,
                cancellationToken
            );
            var metrics = OrganisationObligationSummaryMapper.Map(
                work.OrganisationId,
                work.ObligationYear,
                obligations
            );
            var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
            var nextRefreshAt = NextRefreshAt(work.OrganisationId, work.ObligationYear, utcNow);
            var existingSummary = await dbContext
                .OrganisationObligationSummaries.Find(x =>
                    x.OrganisationId == work.OrganisationId && x.ObligationYear == work.ObligationYear
                )
                .SingleOrDefaultAsync(cancellationToken);
            var summary = new OrganisationObligationSummary
            {
                Id = existingSummary?.Id ?? MongoDB.Bson.ObjectId.GenerateNewId(),
                OrganisationId = work.OrganisationId,
                ObligationYear = work.ObligationYear,
                ObligationCount = metrics.ObligationCount,
                TotalAcceptedTonnage = metrics.TotalAcceptedTonnage,
                TotalObligatedTonnage = metrics.TotalObligatedTonnage,
                RecyclingObligationsMet = metrics.RecyclingObligationsMet,
                ObligationCoveragePercentage = metrics.ObligationCoveragePercentage,
                SourceFingerprint = metrics.SourceFingerprint,
                LastSuccessfulReadAt = utcNow,
                DailyCalculationRunId = existingSummary?.DailyCalculationRunId,
                LastAttemptedAt = utcNow,
                NextRefreshAt = nextRefreshAt,
                RefreshState = OrganisationObligationRefreshState.Ready,
                AttemptCount = 0,
                LastFailure = null,
            };
            var nextWork = work with
            {
                Priority = OrganisationObligationHydrationPriority.ScheduledRefresh,
                NextAttemptAt = nextRefreshAt,
                AttemptCount = 0,
                LastFailure = null,
                LastSuccessfulReadAt = utcNow,
            };

            await Persist(summary, nextWork, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            await RecordFailure(work, exception, cancellationToken);
        }
    }

    private async Task Persist(
        OrganisationObligationSummary summary,
        OrganisationObligationHydrationWork work,
        CancellationToken cancellationToken
    )
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
                await dbContext.OrganisationObligationHydrationWork.ReplaceOneAsync(
                    session,
                    x => x.OrganisationId == work.OrganisationId && x.ObligationYear == work.ObligationYear,
                    work,
                    new ReplaceOptions { IsUpsert = true },
                    token
                );

                return true;
            },
            "persist organisation obligation hydration result",
            cancellationToken
        );
    }

    private async Task RecordFailure(
        OrganisationObligationHydrationWork work,
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
        var existingSummary = await dbContext
            .OrganisationObligationSummaries.Find(x =>
                x.OrganisationId == work.OrganisationId && x.ObligationYear == work.ObligationYear
            )
            .SingleOrDefaultAsync(cancellationToken);
        var summary = (
            existingSummary
            ?? new OrganisationObligationSummary
            {
                OrganisationId = work.OrganisationId,
                ObligationYear = work.ObligationYear,
            }
        ) with
        {
            LastAttemptedAt = utcNow,
            NextRefreshAt = nextAttemptAt,
            RefreshState = OrganisationObligationRefreshState.Failed,
            AttemptCount = attemptCount,
            LastFailure = failure,
        };
        var nextWork = work with
        {
            Priority = OrganisationObligationHydrationPriority.Retry,
            NextAttemptAt = nextAttemptAt,
            AttemptCount = attemptCount,
            LastFailure = failure,
            LastSuccessfulReadAt = existingSummary?.LastSuccessfulReadAt,
        };

        await Persist(summary, nextWork, cancellationToken);
    }

    private TimeSpan RetryDelay(int attemptCount)
    {
        var multiplier = 1L << Math.Min(attemptCount - 1, 20);
        var retryTicks = Math.Min(
            options.Value.InitialRetryDelay.Ticks * multiplier,
            options.Value.MaximumRetryDelay.Ticks
        );

        return TimeSpan.FromTicks(retryTicks);
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
}
