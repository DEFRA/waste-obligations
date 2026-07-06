using Defra.WasteObligations.AuditEvents.Data;
using Defra.WasteObligations.AuditEvents.Entities;
using Defra.WasteObligations.AuditEvents.Metrics;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Defra.WasteObligations.AuditEvents;

public class AuditEventDispatchService(
    IAuditEventDbContext dbContext,
    TimeProvider timeProvider,
    IAuditEventMetrics auditEventMetrics,
    ILogger<AuditEventDispatchService> logger
)
{
    private const string FailedStatus = nameof(AuditEventDispatchStatus.Failed);

    public async Task<IReadOnlyCollection<AuditEvent>> ReadUnsent(
        string processName,
        int batchSize,
        CancellationToken cancellationToken
    )
    {
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        var noDispatch = Builders<AuditEvent>.Filter.Exists(
            AuditEventDispatchFieldNames.DispatchPath(processName),
            false
        );
        var failedAndDue = Builders<AuditEvent>.Filter.And(
            Builders<AuditEvent>.Filter.Eq(AuditEventDispatchFieldNames.DispatchStatusPath(processName), FailedStatus),
            Builders<AuditEvent>.Filter.Lte(AuditEventDispatchFieldNames.DispatchNextAttemptAtPath(processName), utcNow)
        );
        var filter = Builders<AuditEvent>.Filter.Or(noDispatch, failedAndDue);

        // With secondaryPreferred reads this can see stale dispatch state, so an event may be sent more than once.
        // The topic/queue pipeline is at-least-once delivery, and consumers are expected to handle duplicates.
        var auditEvents = await dbContext
            .AuditEvents.Find(filter)
            .SortBy(x => x.Sequence)
            .Limit(batchSize)
            .ToListAsync(cancellationToken);

        auditEventMetrics.DispatchBatchRead(
            processName,
            auditEvents.Count,
            OldestUnsentMilliseconds(utcNow, auditEvents)
        );

        return auditEvents;
    }

    public async Task MarkDispatched(string processName, AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        var dispatch = new AuditEventDispatch
        {
            Status = AuditEventDispatchStatus.Dispatched,
            Date = timeProvider.GetUtcNowWithoutMicroseconds(),
            AttemptCount = ReadAttemptCount(processName, auditEvent) + 1,
        };

        var marked = await Mark(processName, auditEvent, dispatch, "processed", cancellationToken);

        if (marked)
            auditEventMetrics.DispatchDispatched(processName, auditEvent);
    }

    public async Task MarkFailed(
        string processName,
        AuditEvent auditEvent,
        Exception exception,
        int maxDispatchAttempts,
        TimeSpan failedDispatchRetryDelay,
        CancellationToken cancellationToken
    )
    {
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        var attemptCount = ReadAttemptCount(processName, auditEvent) + 1;
        var status =
            attemptCount >= maxDispatchAttempts
                ? AuditEventDispatchStatus.DeadLettered
                : AuditEventDispatchStatus.Failed;

        var dispatch = new AuditEventDispatch
        {
            Status = status,
            Date = utcNow,
            Message = exception.Message,
            AttemptCount = attemptCount,
            NextAttemptAt = status is AuditEventDispatchStatus.Failed ? utcNow.Add(failedDispatchRetryDelay) : null,
        };

        var marked = await Mark(processName, auditEvent, dispatch, "failed", cancellationToken);

        if (marked)
            auditEventMetrics.DispatchFailed(processName, auditEvent, status, exception);
    }

    private async Task<bool> Mark(
        string processName,
        AuditEvent auditEvent,
        AuditEventDispatch dispatch,
        string outcome,
        CancellationToken cancellationToken
    )
    {
        var filter = Builders<AuditEvent>.Filter.And(
            Builders<AuditEvent>.Filter.Eq(x => x.EventId, auditEvent.EventId),
            Builders<AuditEvent>.Filter.Or(
                Builders<AuditEvent>.Filter.Exists(AuditEventDispatchFieldNames.DispatchPath(processName), false),
                Builders<AuditEvent>.Filter.Eq(
                    AuditEventDispatchFieldNames.DispatchStatusPath(processName),
                    FailedStatus
                )
            )
        );

        var update = Builders<AuditEvent>.Update.Set(AuditEventDispatchFieldNames.DispatchPath(processName), dispatch);

        var result = await dbContext.AuditEvents.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

        if (result.ModifiedCount == 0)
        {
            auditEventMetrics.DispatchMarkFailed(processName, auditEvent, outcome);
            logger.LogError(
                "Audit event {EventId} was {Outcome} but could not be marked with the dispatch outcome by {ProcessName}",
                auditEvent.EventId,
                outcome,
                processName
            );

            return false;
        }

        return true;
    }

    private static double? OldestUnsentMilliseconds(DateTime utcNow, IReadOnlyCollection<AuditEvent> auditEvents)
    {
        if (auditEvents.Count == 0)
            return null;

        var oldestRecordedAt = auditEvents.Min(x => x.RecordedAt);

        return Math.Max(0, (utcNow - oldestRecordedAt).TotalMilliseconds);
    }

    private static int ReadAttemptCount(string processName, AuditEvent auditEvent) =>
        auditEvent.Dispatches.TryGetValue(processName, out var dispatch) ? dispatch.AttemptCount : 0;
}
