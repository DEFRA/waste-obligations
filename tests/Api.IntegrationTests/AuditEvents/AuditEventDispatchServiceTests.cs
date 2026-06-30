using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.AuditEvents;
using Defra.WasteObligations.AuditEvents.Data;
using Defra.WasteObligations.AuditEvents.Entities;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Driver;
using NSubstitute;

namespace Defra.WasteObligations.Api.IntegrationTests.AuditEvents;

public class AuditEventDispatchServiceTests : IntegrationTestBase
{
    private const string Analytics = "analytics-dispatch-test";
    private const string SomeOtherProcess = "someOtherProcess";

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
        var failed = CreateAuditEvent(
            "event-5",
            5,
            new Dictionary<string, AuditEventDispatch>
            {
                [Analytics] = Failed(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), "Failed"),
            }
        );
        var undispatched = CreateAuditEvent("event-1", 1);
        var cappedOut = CreateAuditEvent("event-4", 4);

        await AuditEvents.InsertManyAsync(
            [alreadyDispatched, dispatchedForOtherProcess, failed, undispatched, cappedOut],
            cancellationToken: TestContext.Current.CancellationToken
        );

        var subject = CreateSubject();

        var result = await subject.ReadUnsent(Analytics, 2, TestContext.Current.CancellationToken);

        result.Select(x => x.EventId).Should().Equal("event-1", "event-3");
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
                new AuditEventDispatch { Status = AuditEventDispatchStatus.Dispatched, Date = sentAt.UtcDateTime }
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
        var subject = CreateSubject(timeProvider);

        await subject.MarkDispatched(Analytics, auditEvent, TestContext.Current.CancellationToken);

        var result = await AuditEvents
            .Find(x => x.EventId == auditEvent.EventId)
            .SingleAsync(TestContext.Current.CancellationToken);
        result.Dispatches[Analytics].Should().Be(existingDispatch);
    }

    private static AuditEventDispatchService CreateSubject(TimeProvider? timeProvider = null) =>
        new(
            new AuditEventDbContext(GetMongoDatabase()),
            timeProvider ?? new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            Substitute.For<ILogger<AuditEventDispatchService>>()
        );

    private static AuditEvent CreateAuditEvent(
        string eventId,
        long sequence,
        Dictionary<string, AuditEventDispatch>? dispatches = null
    ) => AuditEventFixture.ComplianceDeclaration(eventId, sequence).With(x => x.Dispatches, dispatches ?? []).Create();

    private static AuditEventDispatch Dispatched(DateTime date) =>
        new() { Status = AuditEventDispatchStatus.Dispatched, Date = date };

    private static AuditEventDispatch Failed(DateTime date, string message) =>
        new()
        {
            Status = AuditEventDispatchStatus.Failed,
            Date = date,
            Message = message,
        };
}
