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

    private static ComplianceDeclarationReviewStateInitialRolloutReconciliationWorker CreateSubject(
        IOrganisationEligibilityRefreshLeaseService leaseService,
        IComplianceDeclarationReviewStateBackfillService backfillService,
        RecordingLogger<ComplianceDeclarationReviewStateInitialRolloutReconciliationWorker> logger
    )
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => leaseService);
        services.AddScoped(_ => backfillService);
        var serviceProvider = services.BuildServiceProvider();
        var options = Options.Create(
            new OrganisationEligibilityOptions
            {
                InitialRolloutReconciliationDelaySeconds = 1,
                RefreshLeaseDurationSeconds = 300,
                RefreshLeaseRenewalIntervalSeconds = 60,
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
