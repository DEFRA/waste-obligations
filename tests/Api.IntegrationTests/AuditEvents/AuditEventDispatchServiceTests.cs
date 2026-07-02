using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.AuditEvents;
using Defra.WasteObligations.AuditEvents.Data;
using Defra.WasteObligations.AuditEvents.Entities;
using Defra.WasteObligations.Testing;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Driver;
using NSubstitute;

namespace Defra.WasteObligations.Api.IntegrationTests.AuditEvents;

public class AuditEventDispatchServiceTests : IntegrationTestBase
{
    private const string Analytics = "analytics-dispatch-test";
    private const int MaxDispatchAttempts = 5;
    private const string SomeOtherProcess = "someOtherProcess";
    private static readonly TimeSpan s_failedDispatchRetryDelay = TimeSpan.FromMinutes(1);

    [Fact]
    public async Task ReadUnsent_ShouldReturnEventsNotDispatchedForProcess()
    {
        var alreadyDispatched = CreateAuditEvent(
            "event-2",
            2,
            new Dictionary<string, AuditEventDispatch>
            {
                [Analytics] = Dispatched(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            }
        );
        var dispatchedForOtherProcess = CreateAuditEvent(
            "event-3",
            3,
            new Dictionary<string, AuditEventDispatch>
            {
                [SomeOtherProcess] = Dispatched(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            }
        );
        var failedButNotDue = CreateAuditEvent(
            "event-5",
            5,
            new Dictionary<string, AuditEventDispatch>
            {
                [Analytics] = Failed(
                    new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    "Failed",
                    nextAttemptAt: new DateTime(2026, 1, 1, 0, 1, 0, DateTimeKind.Utc)
                ),
            }
        );
        var failedAndDue = CreateAuditEvent(
            "event-6",
            6,
            new Dictionary<string, AuditEventDispatch>
            {
                [Analytics] = Failed(
                    new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    "Failed",
                    nextAttemptAt: new DateTime(2025, 12, 31, 23, 59, 0, DateTimeKind.Utc)
                ),
            }
        );
        var deadLettered = CreateAuditEvent(
            "event-7",
            7,
            new Dictionary<string, AuditEventDispatch>
            {
                [Analytics] = new AuditEventDispatch
                {
                    Status = AuditEventDispatchStatus.DeadLettered,
                    Date = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    Message = "Dead lettered",
                    AttemptCount = MaxDispatchAttempts,
                },
            }
        );
        var undispatched = CreateAuditEvent("event-1", 1);
        var anotherUndispatched = CreateAuditEvent("event-4", 4);

        await AuditEvents.InsertManyAsync(
            [
                alreadyDispatched,
                dispatchedForOtherProcess,
                failedButNotDue,
                failedAndDue,
                deadLettered,
                undispatched,
                anotherUndispatched,
            ],
            cancellationToken: TestContext.Current.CancellationToken
        );

        var subject = CreateSubject();

        var result = await subject.ReadUnsent(Analytics, 4, TestContext.Current.CancellationToken);

        result.Select(x => x.EventId).Should().Equal("event-1", "event-3", "event-4", "event-6");
    }

    [Fact]
    public async Task MarkDispatched_ShouldWriteTypedDispatch()
    {
        var sentAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(sentAt);
        var auditEvent = CreateAuditEvent("event-1", 1);
        await AuditEvents.InsertOneAsync(auditEvent, cancellationToken: TestContext.Current.CancellationToken);
        var subject = CreateSubject(timeProvider);

        await subject.MarkDispatched(Analytics, auditEvent, TestContext.Current.CancellationToken);

        var result = await AuditEvents
            .Find(x => x.EventId == auditEvent.EventId)
            .SingleAsync(TestContext.Current.CancellationToken);
        result
            .Dispatches[Analytics]
            .Should()
            .BeEquivalentTo(
                new AuditEventDispatch
                {
                    Status = AuditEventDispatchStatus.Dispatched,
                    Date = sentAt.UtcDateTime,
                    AttemptCount = 1,
                }
            );
    }

    [Fact]
    public async Task MarkFailed_ShouldWriteTypedDispatch()
    {
        const string message = "Message too large";

        var failedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(failedAt);
        var auditEvent = CreateAuditEvent("event-1", 1);
        await AuditEvents.InsertOneAsync(auditEvent, cancellationToken: TestContext.Current.CancellationToken);
        var subject = CreateSubject(timeProvider);

        await subject.MarkFailed(
            Analytics,
            auditEvent,
            new InvalidOperationException(message),
            MaxDispatchAttempts,
            s_failedDispatchRetryDelay,
            TestContext.Current.CancellationToken
        );

        var result = await AuditEvents
            .Find(x => x.EventId == auditEvent.EventId)
            .SingleAsync(TestContext.Current.CancellationToken);
        result
            .Dispatches[Analytics]
            .Should()
            .BeEquivalentTo(
                new AuditEventDispatch
                {
                    Status = AuditEventDispatchStatus.Failed,
                    Date = failedAt.UtcDateTime,
                    Message = message,
                    AttemptCount = 1,
                    NextAttemptAt = failedAt.UtcDateTime.Add(s_failedDispatchRetryDelay),
                }
            );
    }

    [Fact]
    public async Task MarkFailed_WhenMaxAttemptsReached_ShouldWriteDeadLetteredDispatch()
    {
        const string message = "Message too large";

        var failedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(failedAt);
        var existingDispatch = Failed(
            new DateTime(2026, 1, 2, 3, 3, 5, DateTimeKind.Utc),
            "Previous failure",
            attemptCount: MaxDispatchAttempts - 1,
            nextAttemptAt: failedAt.UtcDateTime.AddMinutes(-1)
        );
        var auditEvent = CreateAuditEvent(
            "event-1",
            1,
            new Dictionary<string, AuditEventDispatch> { [Analytics] = existingDispatch }
        );
        await AuditEvents.InsertOneAsync(auditEvent, cancellationToken: TestContext.Current.CancellationToken);
        var subject = CreateSubject(timeProvider);

        await subject.MarkFailed(
            Analytics,
            auditEvent,
            new InvalidOperationException(message),
            MaxDispatchAttempts,
            s_failedDispatchRetryDelay,
            TestContext.Current.CancellationToken
        );

        var result = await AuditEvents
            .Find(x => x.EventId == auditEvent.EventId)
            .SingleAsync(TestContext.Current.CancellationToken);
        result
            .Dispatches[Analytics]
            .Should()
            .BeEquivalentTo(
                new AuditEventDispatch
                {
                    Status = AuditEventDispatchStatus.DeadLettered,
                    Date = failedAt.UtcDateTime,
                    Message = message,
                    AttemptCount = MaxDispatchAttempts,
                }
            );
    }

    [Fact]
    public async Task MarkDispatched_WhenFailed_ShouldOverwriteDispatch()
    {
        var sentAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var existingDispatch = Failed(
            new DateTime(2026, 1, 2, 3, 3, 5, DateTimeKind.Utc),
            "Previous failure",
            attemptCount: 1,
            nextAttemptAt: sentAt.UtcDateTime.AddMinutes(-1)
        );
        var auditEvent = CreateAuditEvent(
            "event-1",
            1,
            new Dictionary<string, AuditEventDispatch> { [Analytics] = existingDispatch }
        );
        await AuditEvents.InsertOneAsync(auditEvent, cancellationToken: TestContext.Current.CancellationToken);
        var timeProvider = new FakeTimeProvider(sentAt);
        var subject = CreateSubject(timeProvider);

        await subject.MarkDispatched(Analytics, auditEvent, TestContext.Current.CancellationToken);

        var result = await AuditEvents
            .Find(x => x.EventId == auditEvent.EventId)
            .SingleAsync(TestContext.Current.CancellationToken);
        result
            .Dispatches[Analytics]
            .Should()
            .BeEquivalentTo(
                new AuditEventDispatch
                {
                    Status = AuditEventDispatchStatus.Dispatched,
                    Date = sentAt.UtcDateTime,
                    AttemptCount = 2,
                }
            );
    }

    [Fact]
    public async Task MarkDispatched_WhenAlreadyDispatched_ShouldNotOverwriteDispatch()
    {
        var existingDispatch = Dispatched(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var auditEvent = CreateAuditEvent(
            "event-1",
            1,
            new Dictionary<string, AuditEventDispatch> { [Analytics] = existingDispatch }
        );
        await AuditEvents.InsertOneAsync(auditEvent, cancellationToken: TestContext.Current.CancellationToken);
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var logger = new RecordingLogger<AuditEventDispatchService>();
        var subject = CreateSubject(timeProvider, logger);

        await subject.MarkDispatched(Analytics, auditEvent, TestContext.Current.CancellationToken);

        var result = await AuditEvents
            .Find(x => x.EventId == auditEvent.EventId)
            .SingleAsync(TestContext.Current.CancellationToken);
        result.Dispatches[Analytics].Should().Be(existingDispatch);
        logger
            .Entries.Should()
            .ContainSingle(x =>
                x.Level == LogLevel.Error && x.Message.Contains("could not be marked with the dispatch outcome")
            );
    }

    private static AuditEventDispatchService CreateSubject(
        TimeProvider? timeProvider = null,
        ILogger<AuditEventDispatchService>? logger = null
    ) =>
        new(
            new AuditEventDbContext(GetMongoDatabase()),
            timeProvider ?? new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            logger ?? Substitute.For<ILogger<AuditEventDispatchService>>()
        );

    private static AuditEvent CreateAuditEvent(
        string eventId,
        long sequence,
        Dictionary<string, AuditEventDispatch>? dispatches = null
    ) => AuditEventFixture.ComplianceDeclaration(eventId, sequence).With(x => x.Dispatches, dispatches ?? []).Create();

    private static AuditEventDispatch Dispatched(DateTime date) =>
        new()
        {
            Status = AuditEventDispatchStatus.Dispatched,
            Date = date,
            AttemptCount = 1,
        };

    private static AuditEventDispatch Failed(
        DateTime date,
        string message,
        int attemptCount = 1,
        DateTime? nextAttemptAt = null
    ) =>
        new()
        {
            Status = AuditEventDispatchStatus.Failed,
            Date = date,
            Message = message,
            AttemptCount = attemptCount,
            NextAttemptAt = nextAttemptAt,
        };
}
