using AwesomeAssertions;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.OrganisationObligations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Services.OrganisationObligations;

public class OrganisationObligationHydrationWorkerTests
{
    [Fact]
    public async Task Start_WhenLeaseIsAcquired_ShouldHydrateCurrentComplianceYearAndReleaseLease()
    {
        var leaseService = Substitute.For<IOrganisationObligationHydrationLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var hydrationService = Substitute.For<IOrganisationObligationHydrationService>();
        hydrationService.HydrateDue(2026, Arg.Any<CancellationToken>()).Returns(3);
        var currentComplianceYearProvider = Substitute.For<ICurrentComplianceYearProvider>();
        currentComplianceYearProvider.GetCurrentComplianceYear().Returns(2026);
        var hydrated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        hydrationService
            .HydrateDue(2026, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                hydrated.TrySetResult();
                return Task.FromResult(3);
            });
        var subject = CreateSubject(leaseService, hydrationService, currentComplianceYearProvider);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await hydrated.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        await leaseService.Received(1).TryAcquire(TimeSpan.FromSeconds(300), Arg.Any<CancellationToken>());
        await hydrationService.Received(1).HydrateDue(2026, Arg.Any<CancellationToken>());
        await leaseService.Received(1).Release(CancellationToken.None);
    }

    [Fact]
    public async Task Start_WhenAnotherInstanceHoldsLease_ShouldNotHydrate()
    {
        var leaseService = Substitute.For<IOrganisationObligationHydrationLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(false);
        var hydrationService = Substitute.For<IOrganisationObligationHydrationService>();
        var subject = CreateSubject(leaseService, hydrationService, Substitute.For<ICurrentComplianceYearProvider>());

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        await hydrationService.DidNotReceive().HydrateDue(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await leaseService.DidNotReceive().Release(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_WhenABatchIsFull_ShouldImmediatelyTryAnotherBatch()
    {
        var leaseService = Substitute.For<IOrganisationObligationHydrationLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var hydrationService = Substitute.For<IOrganisationObligationHydrationService>();
        var currentComplianceYearProvider = Substitute.For<ICurrentComplianceYearProvider>();
        currentComplianceYearProvider.GetCurrentComplianceYear().Returns(2026);
        var secondBatchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callCount = 0;
        hydrationService
            .HydrateDue(2026, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 2)
                    secondBatchStarted.TrySetResult();

                return Task.FromResult(callCount == 1 ? 10 : 0);
            });
        var subject = CreateSubject(leaseService, hydrationService, currentComplianceYearProvider);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await secondBatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        await hydrationService.Received(2).HydrateDue(2026, Arg.Any<CancellationToken>());
    }

    private static OrganisationObligationHydrationWorker CreateSubject(
        IOrganisationObligationHydrationLeaseService leaseService,
        IOrganisationObligationHydrationService hydrationService,
        ICurrentComplianceYearProvider currentComplianceYearProvider
    )
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => leaseService);
        services.AddScoped(_ => hydrationService);
        services.AddScoped(_ => currentComplianceYearProvider);
        var serviceProvider = services.BuildServiceProvider();

        return new OrganisationObligationHydrationWorker(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(
                new OrganisationObligationHydrationOptions
                {
                    PollIntervalSeconds = 3600,
                    LeaseDurationSeconds = 300,
                    LeaseRenewalIntervalSeconds = 60,
                }
            ),
            Substitute.For<ILogger<OrganisationObligationHydrationWorker>>()
        );
    }
}
