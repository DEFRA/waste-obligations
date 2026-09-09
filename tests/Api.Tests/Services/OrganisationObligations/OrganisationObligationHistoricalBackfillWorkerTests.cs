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

                return new OrganisationObligationHistoricalBackfillProgress
                {
                    ProcessedCount = 1,
                    RemainingCount = 0,
                    WasEnqueued = true,
                };
            });
        var subject = CreateSubject(store, leaseService, hydrationService);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await processed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        await store.Received(1).Complete(backfill, Arg.Any<CancellationToken>());
        await store.Received(1).MarkEnqueued(backfill, Arg.Any<CancellationToken>());
        await leaseService.Received(1).Release(CancellationToken.None);
    }

    [Fact]
    public async Task Start_WhenNormalPollingIsEnabled_ShouldNotProcessHistoricalBackfill()
    {
        var backfill = Backfill();
        var store = Substitute.For<IOrganisationObligationHistoricalBackfillStore>();
        store.GetNextIncomplete(Arg.Any<CancellationToken>()).Returns(backfill);
        var leaseService = Substitute.For<IOrganisationObligationHistoricalBackfillLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var hydrationService = Substitute.For<IOrganisationObligationHydrationService>();
        var subject = CreateSubject(store, leaseService, hydrationService, pollingEnabled: true);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        await store.DidNotReceive().GetNextIncomplete(Arg.Any<CancellationToken>());
        await hydrationService
            .DidNotReceive()
            .HydrateHistoricalBackfill(
                Arg.Any<OrganisationObligationHistoricalBackfill>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<int?>(),
                Arg.Any<bool>()
            );
        await leaseService.DidNotReceive().TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
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
