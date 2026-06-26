using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.AuditEvents;
using Defra.WasteObligations.AuditEvents.Data;
using Defra.WasteObligations.AuditEvents.Entities;
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
            new Dictionary<string, DateTime> { [Analytics] = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
        var dispatchedForOtherProcess = CreateAuditEvent(
            "event-3",
            3,
            new Dictionary<string, DateTime> { [SomeOtherProcess] = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
        var undispatched = CreateAuditEvent("event-1", 1);
        var cappedOut = CreateAuditEvent("event-4", 4);

        await AuditEvents.InsertManyAsync(
            [alreadyDispatched, dispatchedForOtherProcess, undispatched, cappedOut],
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
        result.Dispatches[Analytics].Should().Be(sentAt.UtcDateTime);
    }

    [Fact]
    public async Task MarkDispatched_WhenAlreadyDispatched_ShouldNotOverwriteDispatch()
    {
        var existingDispatch = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var auditEvent = CreateAuditEvent(
            "event-1",
            1,
            new Dictionary<string, DateTime> { [Analytics] = existingDispatch }
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
        Dictionary<string, DateTime>? dispatches = null
    ) =>
        new()
        {
            EventId = eventId,
            Sequence = sequence,
            Entity = "compliance_declaration",
            EntityId = $"entity-{sequence}",
            Operation = "insert",
            OccurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            RecordedAt = new DateTime(2026, 1, 1, 0, 0, 1, DateTimeKind.Utc),
            Actor = "user@example.com",
            Version = 1,
            SchemaVersion = ComplianceDeclaration.SchemaVersionValue,
            Dispatches = dispatches ?? [],
        };
}
