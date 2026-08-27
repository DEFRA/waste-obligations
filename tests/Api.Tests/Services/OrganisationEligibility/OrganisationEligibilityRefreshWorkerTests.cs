using AwesomeAssertions;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.OrganisationEligibility;
using Defra.WasteObligations.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Services.OrganisationEligibility;

public class OrganisationEligibilityRefreshWorkerTests
{
    [Fact]
    public async Task Start_WhenLeaseAcquired_ShouldRefreshAndReleaseLease()
    {
        var leaseService = Substitute.For<IOrganisationEligibilityRefreshLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var refreshService = Substitute.For<IOrganisationEligibilityRefreshService>();
        var backfillService = CreateBackfillService();
        var refreshed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        refreshService
            .Refresh(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                refreshed.TrySetResult();
                return Task.FromResult(
                    new OrganisationEligibilityRefreshResult
                    {
                        Outcome = OrganisationEligibilityRefreshOutcome.Promoted,
                        ActiveGeneration = "generation",
                        RowCount = 1,
                        ContentFingerprint = "fingerprint",
                    }
                );
            });
        var subject = CreateSubject(leaseService, refreshService, backfillService);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        await leaseService.Received(1).TryAcquire(TimeSpan.FromSeconds(300), Arg.Any<CancellationToken>());
        await refreshService.Received(1).Refresh(Arg.Any<CancellationToken>());
        await backfillService.Received(1).Backfill(Arg.Any<CancellationToken>());
        await leaseService.Received(1).Release(CancellationToken.None);
    }

    [Fact]
    public async Task Start_WhenLeaseIsHeldByAnotherInstance_ShouldNotRefresh()
    {
        var leaseService = Substitute.For<IOrganisationEligibilityRefreshLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(false);
        var refreshService = Substitute.For<IOrganisationEligibilityRefreshService>();
        var subject = CreateSubject(leaseService, refreshService);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        await refreshService.DidNotReceive().Refresh(Arg.Any<CancellationToken>());
        await leaseService.DidNotReceive().Release(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_WhenRefreshOutlivesRenewalInterval_ShouldRenewLease()
    {
        var leaseService = Substitute.For<IOrganisationEligibilityRefreshLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var renewed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        leaseService
            .TryRenew(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                renewed.TrySetResult();
                return Task.FromResult(true);
            });
        var refreshService = Substitute.For<IOrganisationEligibilityRefreshService>();
        var refreshCompletion = new TaskCompletionSource<OrganisationEligibilityRefreshResult>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        refreshService.Refresh(Arg.Any<CancellationToken>()).Returns(refreshCompletion.Task);
        var subject = CreateSubject(leaseService, refreshService, refreshLeaseRenewalIntervalSeconds: 1);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await renewed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        refreshCompletion.TrySetResult(
            new OrganisationEligibilityRefreshResult
            {
                Outcome = OrganisationEligibilityRefreshOutcome.Unchanged,
                ActiveGeneration = "generation",
                RowCount = 1,
                ContentFingerprint = "fingerprint",
            }
        );
        await subject.StopAsync(TestContext.Current.CancellationToken);

        await leaseService.Received(1).TryRenew(TimeSpan.FromSeconds(300), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_WhenBackfillFails_ShouldContinueWithRefreshAndReleaseLease()
    {
        var leaseService = Substitute.For<IOrganisationEligibilityRefreshLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var backfillService = Substitute.For<IComplianceDeclarationReviewStateBackfillService>();
        backfillService
            .Backfill(Arg.Any<CancellationToken>())
            .Returns(
                Task.FromException<ComplianceDeclarationReviewStateBackfillResult>(new InvalidOperationException())
            );
        var refreshService = Substitute.For<IOrganisationEligibilityRefreshService>();
        var refreshed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        refreshService
            .Refresh(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                refreshed.TrySetResult();
                return Task.FromResult(
                    new OrganisationEligibilityRefreshResult
                    {
                        Outcome = OrganisationEligibilityRefreshOutcome.Promoted,
                        ActiveGeneration = "generation",
                        RowCount = 1,
                        ContentFingerprint = "fingerprint",
                    }
                );
            });
        var logger = new RecordingLogger<OrganisationEligibilityRefreshWorker>();
        var subject = CreateSubject(leaseService, refreshService, backfillService, logger: logger);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        logger.Messages.Should().Contain("Compliance declaration review state backfill failed");
        await refreshService.Received(1).Refresh(Arg.Any<CancellationToken>());
        await leaseService.Received(1).Release(CancellationToken.None);
    }

    [Fact]
    public async Task Start_WhenBackfillCreatesStateRows_ShouldLogCompletionAndRefresh()
    {
        var leaseService = Substitute.For<IOrganisationEligibilityRefreshLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var backfillService = Substitute.For<IComplianceDeclarationReviewStateBackfillService>();
        backfillService
            .Backfill(Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(
                    new ComplianceDeclarationReviewStateBackfillResult { AlreadyComplete = false, StateRowCount = 25 }
                )
            );
        var refreshService = Substitute.For<IOrganisationEligibilityRefreshService>();
        var refreshed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        refreshService
            .Refresh(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                refreshed.TrySetResult();
                return Task.FromResult(
                    new OrganisationEligibilityRefreshResult
                    {
                        Outcome = OrganisationEligibilityRefreshOutcome.Promoted,
                        ActiveGeneration = "generation",
                        RowCount = 1,
                        ContentFingerprint = "fingerprint",
                    }
                );
            });
        var logger = new RecordingLogger<OrganisationEligibilityRefreshWorker>();
        var subject = CreateSubject(leaseService, refreshService, backfillService, logger: logger);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        logger.Messages.Should().Contain("Compliance declaration review state backfill completed with 25 rows");
        await refreshService.Received(1).Refresh(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_WhenLeaseRenewalFails_ShouldCancelRefreshAndReleaseLease()
    {
        var leaseService = Substitute.For<IOrganisationEligibilityRefreshLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var renewalAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        leaseService
            .TryRenew(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                renewalAttempted.TrySetResult();
                return Task.FromResult(false);
            });
        var refreshService = Substitute.For<IOrganisationEligibilityRefreshService>();
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshCompletion = new TaskCompletionSource<OrganisationEligibilityRefreshResult>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        refreshService
            .Refresh(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var cancellationToken = callInfo.Arg<CancellationToken>();
                cancellationToken.Register(() =>
                {
                    cancelled.TrySetResult();
                    refreshCompletion.TrySetCanceled(cancellationToken);
                });

                return refreshCompletion.Task;
            });
        var logger = new RecordingLogger<OrganisationEligibilityRefreshWorker>();
        var subject = CreateSubject(
            leaseService,
            refreshService,
            refreshLeaseRenewalIntervalSeconds: 1,
            logger: logger
        );

        await subject.StartAsync(CancellationToken.None);
        await renewalAttempted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await subject.StopAsync(CancellationToken.None);

        logger.Messages.Should().Contain("Organisation eligibility refresh stopped because its lease was not renewed");
        await leaseService.Received(1).TryRenew(TimeSpan.FromSeconds(300), Arg.Any<CancellationToken>());
        await leaseService.Received(1).Release(CancellationToken.None);
    }

    [Fact]
    public async Task Start_WhenRefreshFails_ShouldLogTheFailureAndReleaseLease()
    {
        var leaseService = Substitute.For<IOrganisationEligibilityRefreshLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var refreshService = Substitute.For<IOrganisationEligibilityRefreshService>();
        var attempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        refreshService
            .Refresh(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                attempted.TrySetResult();
                return Task.FromException<OrganisationEligibilityRefreshResult>(new InvalidOperationException());
            });
        var logger = new RecordingLogger<OrganisationEligibilityRefreshWorker>();
        var subject = CreateSubject(leaseService, refreshService, logger: logger);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await attempted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await AsyncWaiter.WaitForAsync(
            () =>
            {
                logger.Messages.Should().Contain("Organisation eligibility refresh failed");
                return Task.CompletedTask;
            },
            timeout: 5,
            delay: TimeSpan.FromMilliseconds(10)
        );
        await subject.StopAsync(TestContext.Current.CancellationToken);

        await leaseService.Received(1).Release(CancellationToken.None);
    }

    [Fact]
    public async Task Start_WhenPollingIsDisabled_ShouldNotAcquireLease()
    {
        var leaseService = Substitute.For<IOrganisationEligibilityRefreshLeaseService>();
        var refreshService = Substitute.For<IOrganisationEligibilityRefreshService>();
        var logger = new RecordingLogger<OrganisationEligibilityRefreshWorker>();
        var subject = CreateSubject(leaseService, refreshService, refreshPollingEnabled: false, logger: logger);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        logger.Messages.Should().ContainSingle("Organisation eligibility refresh polling is off");
        await leaseService.DidNotReceive().TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenPollingIsDisabledAndHostStops_ShouldStopPromptly()
    {
        var subject = CreateTestableSubject(
            Substitute.For<IOrganisationEligibilityRefreshLeaseService>(),
            Substitute.For<IOrganisationEligibilityRefreshService>(),
            refreshPollingEnabled: false
        );
        using var stopping = new CancellationTokenSource();
        await stopping.CancelAsync();

        var act = () => subject.Execute(stopping.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Execute_WhenHostStopsAfterRefresh_ShouldCancelThePollDelay()
    {
        var leaseService = Substitute.For<IOrganisationEligibilityRefreshLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var refreshService = Substitute.For<IOrganisationEligibilityRefreshService>();
        var refreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshCompletion = new TaskCompletionSource<OrganisationEligibilityRefreshResult>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        using var stopping = new CancellationTokenSource();
        refreshService
            .Refresh(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                refreshStarted.TrySetResult();
                return refreshCompletion.Task;
            });
        var subject = CreateTestableSubject(leaseService, refreshService);
        var execution = subject.Execute(stopping.Token);

        await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        refreshCompletion.TrySetResult(
            new OrganisationEligibilityRefreshResult
            {
                Outcome = OrganisationEligibilityRefreshOutcome.Unchanged,
                ActiveGeneration = "generation",
                RowCount = 1,
                ContentFingerprint = "fingerprint",
            }
        );
        await stopping.CancelAsync();

        var act = () => execution;
        await act.Should().ThrowAsync<OperationCanceledException>();
        await leaseService.Received(1).Release(CancellationToken.None);
    }

    private static OrganisationEligibilityRefreshWorker CreateSubject(
        IOrganisationEligibilityRefreshLeaseService leaseService,
        IOrganisationEligibilityRefreshService refreshService,
        IComplianceDeclarationReviewStateBackfillService? backfillService = null,
        bool refreshPollingEnabled = true,
        int refreshLeaseRenewalIntervalSeconds = 60,
        ILogger<OrganisationEligibilityRefreshWorker>? logger = null
    )
    {
        var services = new ServiceCollection();
        backfillService ??= CreateBackfillService();
        services.AddScoped(_ => leaseService);
        services.AddScoped(_ => refreshService);
        services.AddScoped(_ => backfillService);
        var serviceProvider = services.BuildServiceProvider();

        return new OrganisationEligibilityRefreshWorker(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(
                new OrganisationEligibilityOptions
                {
                    RefreshPollingEnabled = refreshPollingEnabled,
                    RefreshPollIntervalSeconds = 3600,
                    RefreshLeaseDurationSeconds = 300,
                    RefreshLeaseRenewalIntervalSeconds = refreshLeaseRenewalIntervalSeconds,
                }
            ),
            logger ?? Substitute.For<ILogger<OrganisationEligibilityRefreshWorker>>()
        );
    }

    private static IComplianceDeclarationReviewStateBackfillService CreateBackfillService()
    {
        var backfillService = Substitute.For<IComplianceDeclarationReviewStateBackfillService>();
        backfillService
            .Backfill(Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(
                    new ComplianceDeclarationReviewStateBackfillResult { AlreadyComplete = true, StateRowCount = 0 }
                )
            );

        return backfillService;
    }

    private static TestableOrganisationEligibilityRefreshWorker CreateTestableSubject(
        IOrganisationEligibilityRefreshLeaseService leaseService,
        IOrganisationEligibilityRefreshService refreshService,
        IComplianceDeclarationReviewStateBackfillService? backfillService = null,
        bool refreshPollingEnabled = true,
        int refreshLeaseRenewalIntervalSeconds = 60,
        ILogger<OrganisationEligibilityRefreshWorker>? logger = null
    )
    {
        var services = new ServiceCollection();
        backfillService ??= CreateBackfillService();
        services.AddScoped(_ => leaseService);
        services.AddScoped(_ => refreshService);
        services.AddScoped(_ => backfillService);
        var serviceProvider = services.BuildServiceProvider();

        return new TestableOrganisationEligibilityRefreshWorker(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(
                new OrganisationEligibilityOptions
                {
                    RefreshPollingEnabled = refreshPollingEnabled,
                    RefreshPollIntervalSeconds = 3600,
                    RefreshLeaseDurationSeconds = 300,
                    RefreshLeaseRenewalIntervalSeconds = refreshLeaseRenewalIntervalSeconds,
                }
            ),
            logger ?? Substitute.For<ILogger<OrganisationEligibilityRefreshWorker>>()
        );
    }

    private sealed class TestableOrganisationEligibilityRefreshWorker(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<OrganisationEligibilityOptions> options,
        ILogger<OrganisationEligibilityRefreshWorker> logger
    ) : OrganisationEligibilityRefreshWorker(serviceScopeFactory, options, logger)
    {
        public Task Execute(CancellationToken stoppingToken) => ExecuteAsync(stoppingToken);
    }
}
