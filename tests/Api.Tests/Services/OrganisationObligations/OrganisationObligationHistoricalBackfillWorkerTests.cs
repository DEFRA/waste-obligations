using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.OrganisationObligations;
using Defra.WasteObligations.Api.Utils.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Services.OrganisationObligations;

public class OrganisationObligationHistoricalBackfillWorkerTests
{
    [Fact]
    public async Task Start_WhenBackfillIsComplete_ShouldMarkItCompleteAndReleaseLease()
    {
        var backfill = Backfill();
        var store = Substitute.For<IOrganisationObligationHistoricalBackfillStore>();
        store.GetNextIncomplete(Arg.Any<CancellationToken>()).Returns(backfill);
        var leaseService = Substitute.For<IOrganisationObligationHistoricalBackfillLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var hydrationService = Substitute.For<IOrganisationObligationHydrationService>();
        var processed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        hydrationService
            .HydrateHistoricalBackfill(backfill, Arg.Any<CancellationToken>(), 10, preserveCurrentYearPacing: false)
            .Returns(_ =>
            {
                processed.TrySetResult();

                return new OrganisationObligationHistoricalBackfillProgress { ProcessedCount = 1, RemainingCount = 0 };
            });
        var subject = CreateSubject(store, leaseService, hydrationService);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await processed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        await store.Received(1).Complete(backfill, Arg.Any<CancellationToken>());
        await leaseService.Received(1).Release(CancellationToken.None);
    }

    [Fact]
    public async Task Start_WhenCurrentYearWorkIsDue_ShouldNotProcessHistoricalBackfill()
    {
        var backfill = Backfill();
        var store = Substitute.For<IOrganisationObligationHistoricalBackfillStore>();
        store.GetNextIncomplete(Arg.Any<CancellationToken>()).Returns(backfill);
        var leaseService = Substitute.For<IOrganisationObligationHistoricalBackfillLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var hydrationService = Substitute.For<IOrganisationObligationHydrationService>();
        var historicalBackfillDeferred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store
            .MarkDeferred(
                backfill,
                OrganisationObligationHistoricalBackfillDeferralReason.CurrentYearWorkDue,
                Arg.Any<CancellationToken>()
            )
            .Returns(_ =>
            {
                historicalBackfillDeferred.TrySetResult();

                return Task.CompletedTask;
            });
        hydrationService.HydrateDue(2026, Arg.Any<CancellationToken>(), 10).Returns(1);
        var subject = CreateSubject(store, leaseService, hydrationService, pollingEnabled: true);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await historicalBackfillDeferred.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        await hydrationService
            .DidNotReceive()
            .HydrateHistoricalBackfill(
                Arg.Any<OrganisationObligationHistoricalBackfill>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<int?>(),
                Arg.Any<bool>()
            );
        await store
            .Received(1)
            .MarkDeferred(
                backfill,
                OrganisationObligationHistoricalBackfillDeferralReason.CurrentYearWorkDue,
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Start_WhenCurrentYearWorkIsNotDue_ShouldProcessOneHistoricalBackfillItem()
    {
        var backfill = Backfill();
        var store = Substitute.For<IOrganisationObligationHistoricalBackfillStore>();
        store.GetNextIncomplete(Arg.Any<CancellationToken>()).Returns(backfill);
        var leaseService = Substitute.For<IOrganisationObligationHistoricalBackfillLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var hydrationService = Substitute.For<IOrganisationObligationHydrationService>();
        hydrationService.HydrateDue(2026, Arg.Any<CancellationToken>(), 10).Returns(0);
        var historicalItemHydrated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        hydrationService
            .HydrateHistoricalBackfill(backfill, Arg.Any<CancellationToken>(), 1, preserveCurrentYearPacing: true)
            .Returns(_ =>
            {
                historicalItemHydrated.TrySetResult();

                return new OrganisationObligationHistoricalBackfillProgress { ProcessedCount = 1, RemainingCount = 1 };
            });
        var subject = CreateSubject(store, leaseService, hydrationService, pollingEnabled: true);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await historicalItemHydrated.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        await hydrationService.Received(1).HydrateDue(2026, Arg.Any<CancellationToken>(), 10);
        await hydrationService
            .Received(1)
            .HydrateHistoricalBackfill(backfill, Arg.Any<CancellationToken>(), 1, preserveCurrentYearPacing: true);
    }

    [Fact]
    public async Task Start_WhenAnotherInstanceHoldsLease_ShouldNotHydrateOrComplete()
    {
        var backfill = Backfill();
        var store = Substitute.For<IOrganisationObligationHistoricalBackfillStore>();
        store.GetNextIncomplete(Arg.Any<CancellationToken>()).Returns(backfill);
        var leaseService = Substitute.For<IOrganisationObligationHistoricalBackfillLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(false);
        var hydrationService = Substitute.For<IOrganisationObligationHydrationService>();
        var metrics = Substitute.For<IOrganisationObligationHydrationMetrics>();
        var historicalBackfillDeferred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store
            .MarkDeferred(
                backfill,
                OrganisationObligationHistoricalBackfillDeferralReason.LeaseHeld,
                Arg.Any<CancellationToken>()
            )
            .Returns(_ =>
            {
                historicalBackfillDeferred.TrySetResult();

                return Task.CompletedTask;
            });
        var subject = CreateSubject(store, leaseService, hydrationService, metrics: metrics);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await historicalBackfillDeferred.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        await hydrationService
            .DidNotReceive()
            .HydrateHistoricalBackfill(
                Arg.Any<OrganisationObligationHistoricalBackfill>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<int?>(),
                Arg.Any<bool>()
            );
        await store
            .DidNotReceive()
            .Complete(Arg.Any<OrganisationObligationHistoricalBackfill>(), Arg.Any<CancellationToken>());
        await store
            .Received(1)
            .MarkDeferred(
                backfill,
                OrganisationObligationHistoricalBackfillDeferralReason.LeaseHeld,
                Arg.Any<CancellationToken>()
            );
        metrics.Received(1).LeaseNotAcquired();
        await leaseService.DidNotReceive().Release(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_WhenNoBackfillExists_ShouldNotAcquireLease()
    {
        var store = Substitute.For<IOrganisationObligationHistoricalBackfillStore>();
        var leaseService = Substitute.For<IOrganisationObligationHistoricalBackfillLeaseService>();
        var hydrationService = Substitute.For<IOrganisationObligationHydrationService>();
        var subject = CreateSubject(store, leaseService, hydrationService, pollingEnabled: true);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        await leaseService.DidNotReceive().TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    private static OrganisationObligationHistoricalBackfillWorker CreateSubject(
        IOrganisationObligationHistoricalBackfillStore store,
        IOrganisationObligationHistoricalBackfillLeaseService leaseService,
        IOrganisationObligationHydrationService hydrationService,
        bool pollingEnabled = false,
        IOrganisationObligationHydrationMetrics? metrics = null
    )
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => store);
        services.AddScoped(_ => leaseService);
        services.AddScoped(_ => hydrationService);
        services.AddScoped(_ => metrics ?? Substitute.For<IOrganisationObligationHydrationMetrics>());
        var currentObligationYearProvider = Substitute.For<ICurrentObligationYearProvider>();
        currentObligationYearProvider.GetCurrentObligationYear().Returns(2026);
        services.AddScoped(_ => currentObligationYearProvider);
        var serviceProvider = services.BuildServiceProvider();

        return new OrganisationObligationHistoricalBackfillWorker(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(
                new OrganisationObligationHydrationOptions
                {
                    PollingEnabled = pollingEnabled,
                    PollIntervalSeconds = 3600,
                    LeaseDurationSeconds = 300,
                    LeaseRenewalIntervalSeconds = 60,
                }
            ),
            Substitute.For<ILogger<OrganisationObligationHistoricalBackfillWorker>>()
        );
    }

    private static OrganisationObligationHistoricalBackfill Backfill() =>
        new()
        {
            ObligationYear = 2025,
            OrganisationIds = [Guid.NewGuid()],
            RequestedAt = new DateTime(2026, 9, 8, 16, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 9, 8, 16, 0, 0, DateTimeKind.Utc),
        };
}
