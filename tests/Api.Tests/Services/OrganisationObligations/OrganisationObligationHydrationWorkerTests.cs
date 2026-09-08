using AwesomeAssertions;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.OrganisationObligations;
using Defra.WasteObligations.Api.Utils.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Services.OrganisationObligations;

public class OrganisationObligationHydrationWorkerTests
{
    [Fact]
    public async Task Start_WhenLeaseIsAcquired_ShouldHydrateCurrentObligationYearAndReleaseLease()
    {
        var leaseService = Substitute.For<IOrganisationObligationHydrationLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var hydrationService = Substitute.For<IOrganisationObligationHydrationService>();
        hydrationService.HydrateDue(2026, Arg.Any<CancellationToken>(), 10).Returns(3);
        var currentObligationYearProvider = Substitute.For<ICurrentObligationYearProvider>();
        currentObligationYearProvider.GetCurrentObligationYear().Returns(2026);
        var hydrated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        hydrationService
            .HydrateDue(2026, Arg.Any<CancellationToken>(), 10)
            .Returns(_ =>
            {
                hydrated.TrySetResult();
                return Task.FromResult(3);
            });
        var subject = CreateSubject(leaseService, hydrationService, currentObligationYearProvider);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await hydrated.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        await leaseService.Received(1).TryAcquire(TimeSpan.FromSeconds(300), Arg.Any<CancellationToken>());
        await hydrationService.Received(1).HydrateDue(2026, Arg.Any<CancellationToken>(), 10);
        await leaseService.Received(1).Release(CancellationToken.None);
    }

    [Fact]
    public async Task Start_WhenAnotherInstanceHoldsLease_ShouldNotHydrate()
    {
        var leaseService = Substitute.For<IOrganisationObligationHydrationLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(false);
        var hydrationService = Substitute.For<IOrganisationObligationHydrationService>();
        var metrics = Substitute.For<IOrganisationObligationHydrationMetrics>();
        var subject = CreateSubject(
            leaseService,
            hydrationService,
            Substitute.For<ICurrentObligationYearProvider>(),
            metrics: metrics
        );

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        await hydrationService
            .DidNotReceive()
            .HydrateDue(Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<int?>());
        await leaseService.DidNotReceive().Release(Arg.Any<CancellationToken>());
        metrics.Received(1).LeaseNotAcquired();
    }

    [Fact]
    public async Task Start_WhenPollingIsDisabled_ShouldNotAcquireLease()
    {
        var leaseService = Substitute.For<IOrganisationObligationHydrationLeaseService>();
        var hydrationService = Substitute.For<IOrganisationObligationHydrationService>();
        var currentObligationYearProvider = Substitute.For<ICurrentObligationYearProvider>();
        var subject = CreateSubject(
            leaseService,
            hydrationService,
            currentObligationYearProvider,
            pollingEnabled: false
        );

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        await leaseService.DidNotReceive().TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await hydrationService
            .DidNotReceive()
            .HydrateDue(Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<int?>());
    }

    [Fact]
    public async Task Start_WhenABatchIsFull_ShouldImmediatelyTryAnotherBatch()
    {
        var leaseService = Substitute.For<IOrganisationObligationHydrationLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var hydrationService = Substitute.For<IOrganisationObligationHydrationService>();
        var currentObligationYearProvider = Substitute.For<ICurrentObligationYearProvider>();
        currentObligationYearProvider.GetCurrentObligationYear().Returns(2026);
        var secondBatchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callCount = 0;
        hydrationService
            .HydrateDue(2026, Arg.Any<CancellationToken>(), 10)
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 2)
                    secondBatchStarted.TrySetResult();

                return Task.FromResult(callCount == 1 ? 10 : 0);
            });
        var subject = CreateSubject(leaseService, hydrationService, currentObligationYearProvider);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await secondBatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        await hydrationService.Received(2).HydrateDue(2026, Arg.Any<CancellationToken>(), 10);
    }

    [Fact]
    public async Task Start_DuringJanuary_ShouldOnlyHydrateTheCurrentObligationYear()
    {
        var leaseService = Substitute.For<IOrganisationObligationHydrationLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var hydrationService = Substitute.For<IOrganisationObligationHydrationService>();
        var currentYearHydrated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        hydrationService
            .HydrateDue(2026, Arg.Any<CancellationToken>(), 10)
            .Returns(_ =>
            {
                currentYearHydrated.TrySetResult();

                return Task.FromResult(1);
            });
        var currentObligationYearProvider = Substitute.For<ICurrentObligationYearProvider>();
        currentObligationYearProvider.GetCurrentObligationYear().Returns(2026);
        var subject = CreateSubject(leaseService, hydrationService, currentObligationYearProvider);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await currentYearHydrated.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        await hydrationService.DidNotReceive().HydrateDue(2027, Arg.Any<CancellationToken>(), Arg.Any<int?>());
    }

    private static OrganisationObligationHydrationWorker CreateSubject(
        IOrganisationObligationHydrationLeaseService leaseService,
        IOrganisationObligationHydrationService hydrationService,
        ICurrentObligationYearProvider currentObligationYearProvider,
        bool pollingEnabled = true,
        IOrganisationObligationHydrationMetrics? metrics = null
    )
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => leaseService);
        services.AddScoped(_ => hydrationService);
        services.AddScoped(_ => currentObligationYearProvider);
        var serviceProvider = services.BuildServiceProvider();

        return new OrganisationObligationHydrationWorker(
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
            metrics ?? Substitute.For<IOrganisationObligationHydrationMetrics>(),
            Substitute.For<ILogger<OrganisationObligationHydrationWorker>>()
        );
    }
}
