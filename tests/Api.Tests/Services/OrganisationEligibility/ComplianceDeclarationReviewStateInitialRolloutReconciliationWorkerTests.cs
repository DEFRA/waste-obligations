using AwesomeAssertions;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.OrganisationEligibility;
using Defra.WasteObligations.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Services.OrganisationEligibility;

public class ComplianceDeclarationReviewStateInitialRolloutReconciliationWorkerTests
{
    [Fact]
    public async Task Start_WhenDelayExpires_ShouldReconcileOnceAndReleaseLease()
    {
        var leaseService = Substitute.For<IOrganisationEligibilityRefreshLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var backfillService = Substitute.For<IComplianceDeclarationReviewStateBackfillService>();
        var reconciled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        backfillService
            .ReconcileInitialRollout(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                reconciled.TrySetResult();

                return Task.FromResult(
                    new ComplianceDeclarationReviewStateBackfillResult { AlreadyComplete = false, StateRowCount = 2 }
                );
            });
        var logger = new RecordingLogger<ComplianceDeclarationReviewStateInitialRolloutReconciliationWorker>();
        var subject = CreateSubject(leaseService, backfillService, logger);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        await backfillService.DidNotReceive().ReconcileInitialRollout(Arg.Any<CancellationToken>());
        await reconciled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        logger
            .Messages.Should()
            .Contain("Compliance declaration review state initial rollout reconciliation completed with 2 rows");
        await backfillService.Received(1).ReconcileInitialRollout(Arg.Any<CancellationToken>());
        await leaseService.Received(1).TryAcquire(TimeSpan.FromSeconds(300), Arg.Any<CancellationToken>());
        await leaseService.Received(1).Release(CancellationToken.None);
    }

    [Fact]
    public async Task Start_WhenAnotherInstanceHoldsLease_ShouldNotReconcileOrReleaseLease()
    {
        var leaseService = Substitute.For<IOrganisationEligibilityRefreshLeaseService>();
        var acquisitionAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        leaseService
            .TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                acquisitionAttempted.TrySetResult();

                return Task.FromResult(false);
            });
        var backfillService = Substitute.For<IComplianceDeclarationReviewStateBackfillService>();
        var logger = new RecordingLogger<ComplianceDeclarationReviewStateInitialRolloutReconciliationWorker>();
        var subject = CreateSubject(leaseService, backfillService, logger, initialRolloutReconciliationDelaySeconds: 0);

        await subject.StartAsync(CancellationToken.None);
        await acquisitionAttempted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await AsyncWaiter.WaitForAsync(
            () =>
            {
                logger
                    .Messages.Should()
                    .Contain(
                        "Compliance declaration review state initial rollout reconciliation skipped because another instance holds the lease"
                    );

                return Task.CompletedTask;
            },
            timeout: 5,
            delay: TimeSpan.FromMilliseconds(10)
        );
        await subject.StopAsync(CancellationToken.None);

        await backfillService.DidNotReceive().ReconcileInitialRollout(Arg.Any<CancellationToken>());
        await leaseService.DidNotReceive().Release(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_WhenLeaseIsRenewedThenLost_ShouldCancelReconciliationAndReleaseLease()
    {
        var leaseService = Substitute.For<IOrganisationEligibilityRefreshLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var leaseLost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var renewalAttemptCount = 0;
        leaseService
            .TryRenew(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var renewed = Interlocked.Increment(ref renewalAttemptCount) == 1;
                if (!renewed)
                    leaseLost.TrySetResult();

                return Task.FromResult(renewed);
            });
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        leaseService
            .Release(CancellationToken.None)
            .Returns(_ =>
            {
                released.TrySetResult();

                return Task.CompletedTask;
            });
        var backfillService = Substitute.For<IComplianceDeclarationReviewStateBackfillService>();
        var reconciliationCompletion = new TaskCompletionSource<ComplianceDeclarationReviewStateBackfillResult>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var reconciliationCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        backfillService
            .ReconcileInitialRollout(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var cancellationToken = callInfo.Arg<CancellationToken>();
                cancellationToken.Register(() =>
                {
                    reconciliationCancelled.TrySetResult();
                    reconciliationCompletion.TrySetCanceled(cancellationToken);
                });

                return reconciliationCompletion.Task;
            });
        var logger = new RecordingLogger<ComplianceDeclarationReviewStateInitialRolloutReconciliationWorker>();
        var subject = CreateSubject(
            leaseService,
            backfillService,
            logger,
            initialRolloutReconciliationDelaySeconds: 0,
            refreshLeaseRenewalIntervalSeconds: 1
        );

        await subject.StartAsync(CancellationToken.None);
        await leaseLost.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await reconciliationCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await released.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await subject.StopAsync(CancellationToken.None);

        logger
            .Messages.Should()
            .Contain(
                "Compliance declaration review state initial rollout reconciliation stopped because its lease was not renewed"
            );
        await leaseService.Received(2).TryRenew(TimeSpan.FromSeconds(300), Arg.Any<CancellationToken>());
        await leaseService.Received(1).Release(CancellationToken.None);
    }

    [Fact]
    public async Task Start_WhenReconciliationFails_ShouldRetryAndReleaseEachLease()
    {
        var leaseService = Substitute.For<IOrganisationEligibilityRefreshLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var secondRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCount = 0;
        leaseService
            .Release(CancellationToken.None)
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref releaseCount) == 2)
                    secondRelease.TrySetResult();

                return Task.CompletedTask;
            });
        var backfillService = Substitute.For<IComplianceDeclarationReviewStateBackfillService>();
        var reconciliationAttemptCount = 0;
        backfillService
            .ReconcileInitialRollout(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref reconciliationAttemptCount) == 1)
                {
                    return Task.FromException<ComplianceDeclarationReviewStateBackfillResult>(
                        new InvalidOperationException("Mongo is unavailable")
                    );
                }

                return Task.FromResult(
                    new ComplianceDeclarationReviewStateBackfillResult { AlreadyComplete = false, StateRowCount = 2 }
                );
            });
        var logger = new RecordingLogger<ComplianceDeclarationReviewStateInitialRolloutReconciliationWorker>();
        var subject = CreateSubject(leaseService, backfillService, logger, initialRolloutReconciliationDelaySeconds: 0);

        await subject.StartAsync(CancellationToken.None);
        await secondRelease.Task.WaitAsync(TimeSpan.FromSeconds(8), TestContext.Current.CancellationToken);
        await subject.StopAsync(CancellationToken.None);

        logger.Messages.Should().Contain("Compliance declaration review state initial rollout reconciliation failed");
        await backfillService.Received(2).ReconcileInitialRollout(Arg.Any<CancellationToken>());
        await leaseService.Received(2).TryAcquire(TimeSpan.FromSeconds(300), Arg.Any<CancellationToken>());
        await leaseService.Received(2).Release(CancellationToken.None);
    }

    private static ComplianceDeclarationReviewStateInitialRolloutReconciliationWorker CreateSubject(
        IOrganisationEligibilityRefreshLeaseService leaseService,
        IComplianceDeclarationReviewStateBackfillService backfillService,
        RecordingLogger<ComplianceDeclarationReviewStateInitialRolloutReconciliationWorker> logger,
        int initialRolloutReconciliationDelaySeconds = 1,
        int refreshLeaseRenewalIntervalSeconds = 60
    )
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => leaseService);
        services.AddScoped(_ => backfillService);
        var serviceProvider = services.BuildServiceProvider();
        var options = Options.Create(
            new OrganisationEligibilityOptions
            {
                InitialRolloutReconciliationDelaySeconds = initialRolloutReconciliationDelaySeconds,
                RefreshLeaseDurationSeconds = 300,
                RefreshLeaseRenewalIntervalSeconds = refreshLeaseRenewalIntervalSeconds,
            }
        );
        var serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        return new ComplianceDeclarationReviewStateInitialRolloutReconciliationWorker(
            serviceScopeFactory,
            options,
            logger
        );
    }
}
