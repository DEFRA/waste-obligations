using AwesomeAssertions;
using Defra.WasteObligations.AuditEvents;
using Defra.WasteObligations.AuditEvents.Analytics;
using Defra.WasteObligations.AuditEvents.Data;
using Defra.WasteObligations.AuditEvents.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Driver;
using NSubstitute;

namespace Defra.WasteObligations.Api.IntegrationTests.AuditEvents.Analytics;

public class AnalyticsAuditEventProcessorTests : IntegrationTestBase
{
    private const string Analytics = "analytics-test";

    [Fact]
    public async Task Start_WhenAuditEventIsUnsent_ShouldSendAndMarkDispatched()
    {
        var database = CreateProcessorDatabase();
        var auditEvents = database.GetCollection<AuditEvent>(nameof(AuditEvent));
        var sentAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(sentAt);
        var sender = new RecordingAnalyticsEventSender();
        var auditEvent = CreateAuditEvent("event-1", 1);
        await auditEvents.InsertOneAsync(auditEvent, cancellationToken: TestContext.Current.CancellationToken);
        var subject = CreateSubject(database, timeProvider, sender);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await sender.WaitForSend(TestContext.Current.CancellationToken);

        sender.SentEvents.Single().Should().BeEquivalentTo(auditEvent.ToAnalyticsEvent());
        await AsyncWaiter.WaitForAsync(
            async () =>
            {
                var result = await auditEvents
                    .Find(x => x.EventId == auditEvent.EventId)
                    .SingleAsync(TestContext.Current.CancellationToken);
                result.Dispatches[Analytics].Should().Be(sentAt.UtcDateTime);
            },
            timeout: 5,
            delay: TimeSpan.FromMilliseconds(50)
        );
        await subject.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Start_WhenAuditEventAlreadyDispatched_ShouldNotSend()
    {
        var database = CreateProcessorDatabase();
        var auditEvents = database.GetCollection<AuditEvent>(nameof(AuditEvent));
        var auditEvent = CreateAuditEvent(
            "event-1",
            1,
            new Dictionary<string, DateTime> { [Analytics] = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
        await auditEvents.InsertOneAsync(auditEvent, cancellationToken: TestContext.Current.CancellationToken);
        var sender = new RecordingAnalyticsEventSender();
        var subject = CreateSubject(database, new FakeTimeProvider(), sender);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        sender.SentEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Start_WhenSenderThrows_ShouldContinueUntilStopped()
    {
        var database = CreateProcessorDatabase();
        var auditEvents = database.GetCollection<AuditEvent>(nameof(AuditEvent));
        var auditEvent = CreateAuditEvent("event-1", 1);
        await auditEvents.InsertOneAsync(auditEvent, cancellationToken: TestContext.Current.CancellationToken);
        var sender = new RecordingAnalyticsEventSender();
        sender.OnSend = (_, cancellationToken) =>
        {
            if (sender.SentEvents.Count == 1)
                throw new InvalidOperationException("Sender failed");

            return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        };
        var subject = CreateSubject(database, new FakeTimeProvider(), sender);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await sender.WaitForSend(TestContext.Current.CancellationToken);
        var act = () => subject.StopAsync(TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
        sender.SentEvents.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Start_WhenLeaseRenewalFails_ShouldStopProcessingRemainingEvents()
    {
        const string anotherInstance = "another-instance";

        var database = CreateProcessorDatabase();
        var auditEvents = database.GetCollection<AuditEvent>(nameof(AuditEvent));
        var leases = database.GetCollection<AuditEventDispatchLease>(nameof(AuditEventDispatchLease));
        var firstAuditEvent = CreateAuditEvent("event-1", 1);
        var secondAuditEvent = CreateAuditEvent("event-2", 2);
        var leaseOwnerChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await auditEvents.InsertManyAsync(
            [firstAuditEvent, secondAuditEvent],
            cancellationToken: TestContext.Current.CancellationToken
        );

        var sender = new RecordingAnalyticsEventSender();
        sender.OnSend = async (_, cancellationToken) =>
        {
            await leases.UpdateOneAsync(
                x => x.Id == Analytics,
                Builders<AuditEventDispatchLease>.Update.Set(x => x.Owner, anotherInstance),
                cancellationToken: cancellationToken
            );
            leaseOwnerChanged.TrySetResult();
        };
        var subject = CreateSubject(database, new FakeTimeProvider(), sender);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await sender.WaitForSend(TestContext.Current.CancellationToken);
        await leaseOwnerChanged.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await AsyncWaiter.WaitForAsync(
            async () =>
            {
                var firstResult = await auditEvents
                    .Find(x => x.EventId == firstAuditEvent.EventId)
                    .SingleAsync(TestContext.Current.CancellationToken);
                firstResult.Dispatches.Should().ContainKey(Analytics);
            },
            timeout: 5,
            delay: TimeSpan.FromMilliseconds(50)
        );
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        sender.SentEvents.Should().ContainSingle();
        sender.SentEvents.Single().EventId.Should().Be(firstAuditEvent.EventId);
        var secondResult = await auditEvents
            .Find(x => x.EventId == secondAuditEvent.EventId)
            .SingleAsync(TestContext.Current.CancellationToken);
        secondResult.Dispatches.Should().NotContainKey(Analytics);
    }

    private static AnalyticsAuditEventProcessor CreateSubject(
        IMongoDatabase database,
        TimeProvider timeProvider,
        IAnalyticsEventSender sender
    )
    {
        var services = new ServiceCollection();
        services.AddSingleton(database);
        services.AddSingleton(timeProvider);
        services.AddSingleton(sender);
        services.AddLogging();
        services.AddScoped<IAuditEventDbContext, AuditEventDbContext>();
        services.AddScoped<AuditEventLeaseService>();
        services.AddScoped<AuditEventDispatchService>();
        var serviceProvider = services.BuildServiceProvider();

        return new AnalyticsAuditEventProcessor(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(
                new AnalyticsAuditEventProcessorOptions
                {
                    ProcessName = Analytics,
                    TopicArn = "arn:aws:sns:eu-west-2:000000000000:waste_obligations_analytics_events",
                    BatchSize = 25,
                    PollIntervalSeconds = 0,
                    PollJitterSeconds = 0,
                    LeaseDurationSeconds = 60,
                }
            ),
            Substitute.For<ILogger<AnalyticsAuditEventProcessor>>()
        );
    }

    private static IMongoDatabase CreateProcessorDatabase() =>
        GetMongoDatabase().Client.GetDatabase($"wo-processor-tests-{Guid.NewGuid():N}");

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
            SchemaVersion = "compliance_declaration.v1",
            Dispatches = dispatches ?? [],
        };

    private sealed class RecordingAnalyticsEventSender : IAnalyticsEventSender
    {
        private readonly TaskCompletionSource _sent = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<AnalyticsEvent> SentEvents { get; } = [];

        public Func<AnalyticsEvent, CancellationToken, Task>? OnSend { get; set; }

        public async Task WaitForSend(CancellationToken cancellationToken) =>
            await _sent.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

        public async Task Send(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken)
        {
            SentEvents.Add(analyticsEvent);
            _sent.TrySetResult();

            if (OnSend is not null)
            {
                await OnSend(analyticsEvent, cancellationToken);
            }
        }
    }
}
