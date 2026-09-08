using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.OrganisationEligibility;
using Defra.WasteObligations.Api.Services.OrganisationObligations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Defra.WasteObligations.Api.IntegrationTests.Services;

public class UnsubmittedPollingStatusServiceTests : IntegrationTestBase
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 9, 6, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Get_WhenPollingDataExists_ShouldReturnConfigurationAndCurrentMaterialisedState()
    {
        const string activeGeneration = "active-generation";
        var eligibleOrganisationId = Guid.NewGuid();
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        await OrganisationEligibilitySnapshots.InsertOneAsync(
            new OrganisationEligibilitySnapshot
            {
                Id = OrganisationEligibilitySnapshot.SnapshotId,
                ActiveGeneration = activeGeneration,
                ActiveContentFingerprint = "fingerprint",
                ActiveRowCount = 3,
                ActiveGenerationPromotedAt = utcNow.AddMinutes(-30),
                LastVerifiedAt = utcNow.AddMinutes(-1),
                MaterialisedStateVersion = 3,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        await OrganisationComplianceDeclarationEligibilities.InsertManyAsync(
            [
                Eligibility(
                    activeGeneration,
                    OrganisationRegistrationStatus.Registered,
                    OrganisationReferenceNumberResolutionState.Resolved,
                    isVisible: true,
                    organisationId: eligibleOrganisationId
                ),
                Eligibility(
                    activeGeneration,
                    OrganisationRegistrationStatus.Registered,
                    OrganisationReferenceNumberResolutionState.Resolved,
                    isVisible: true,
                    organisationId: eligibleOrganisationId,
                    registrationType: RegistrationType.ComplianceScheme
                ),
                Eligibility(
                    activeGeneration,
                    OrganisationRegistrationStatus.Cancelled,
                    OrganisationReferenceNumberResolutionState.Pending,
                    isVisible: false,
                    obligationYear: 2025
                ),
            ],
            cancellationToken: TestContext.Current.CancellationToken
        );
        await OrganisationObligationSummaries.InsertManyAsync(
            [
                Summary(
                    refreshState: OrganisationObligationRefreshState.Pending,
                    nextRefreshAt: utcNow.AddMinutes(-15),
                    lastSuccessfulReadAt: null
                ),
                Summary(
                    refreshState: OrganisationObligationRefreshState.Ready,
                    nextRefreshAt: utcNow.AddMinutes(15),
                    lastSuccessfulReadAt: utcNow.AddMinutes(-5)
                ),
                Summary(
                    refreshState: OrganisationObligationRefreshState.Failed,
                    nextRefreshAt: utcNow.AddMinutes(-15),
                    lastSuccessfulReadAt: utcNow.AddMinutes(-30),
                    isHydrationActive: false
                ),
                Summary(
                    refreshState: OrganisationObligationRefreshState.Ready,
                    nextRefreshAt: utcNow.AddMinutes(15),
                    lastSuccessfulReadAt: utcNow.AddMinutes(-5),
                    obligationYear: 2025
                ),
            ],
            cancellationToken: TestContext.Current.CancellationToken
        );
        await OrganisationWorkerLeases.InsertManyAsync(
            [
                new BackgroundWorkerLease
                {
                    Id = BackgroundWorkerLease.OrganisationEligibilityRefreshLeaseId,
                    ExpiresAt = utcNow.AddMinutes(1),
                    CreatedAt = utcNow.AddMinutes(-1),
                    UpdatedAt = utcNow,
                },
                new BackgroundWorkerLease
                {
                    Id = BackgroundWorkerLease.OrganisationObligationHydrationLeaseId,
                    ExpiresAt = utcNow.AddMinutes(-1),
                    CreatedAt = utcNow.AddMinutes(-10),
                    UpdatedAt = utcNow.AddMinutes(-2),
                    LastReleasedAt = utcNow.AddMinutes(-2),
                },
            ],
            cancellationToken: TestContext.Current.CancellationToken
        );
        await OrganisationObligationHistoricalBackfills.InsertManyAsync(
            [
                new OrganisationObligationHistoricalBackfill
                {
                    ObligationYear = 2023,
                    OrganisationIds = [Guid.NewGuid()],
                    RequestedAt = utcNow.AddMinutes(-30),
                    UpdatedAt = utcNow.AddMinutes(-1),
                    CompletedAt = utcNow.AddMinutes(-1),
                    DeferralReason = OrganisationObligationHistoricalBackfillDeferralReason.LeaseHeld,
                    DeferredAt = utcNow.AddMinutes(-2),
                },
                new OrganisationObligationHistoricalBackfill
                {
                    ObligationYear = 2024,
                    OrganisationIds = [Guid.NewGuid(), Guid.NewGuid()],
                    RequestedAt = utcNow.AddMinutes(-10),
                    UpdatedAt = utcNow.AddMinutes(-5),
                    DeferralReason = OrganisationObligationHistoricalBackfillDeferralReason.CurrentYearWorkDue,
                    DeferredAt = utcNow.AddMinutes(-1),
                },
                new OrganisationObligationHistoricalBackfill
                {
                    ObligationYear = 2025,
                    OrganisationIds = [Guid.NewGuid()],
                    RequestedAt = utcNow.AddMinutes(-20),
                    UpdatedAt = utcNow.AddMinutes(-1),
                },
            ],
            cancellationToken: TestContext.Current.CancellationToken
        );
        var subject = await CreateSubject();

        var result = await subject.Get(TestContext.Current.CancellationToken);

        result.Eligibility.ActiveGeneration.Should().Be(activeGeneration);
        result.Eligibility.ActiveRowCount.Should().Be(3);
        result.Eligibility.VisibleRowCount.Should().Be(2);
        result.Eligibility.HydrationEligibleOrganisationCount.Should().Be(1);
        result
            .Eligibility.MaterialisedObligationYears.Should()
            .BeEquivalentTo([new { ObligationYear = 2025, RowCount = 1 }, new { ObligationYear = 2026, RowCount = 2 }]);
        result
            .Eligibility.ReferenceResolutionStates.Should()
            .Contain(x => x.State == "Resolved" && x.Count == 2)
            .And.Contain(x => x.State == "Pending" && x.Count == 1);
        result.Eligibility.Lease.IsHeld.Should().BeTrue();
        result.ObligationHydration.MaxDownstreamRequestsPerMinute.Should().Be(200);
        result.ObligationHydration.TotalMinimumFullRefreshMinutes.Should().Be(0.01);
        result.ObligationHydration.DesiredRequestsPerMinute.Should().Be(2);
        result.ObligationHydration.EffectiveRequestsPerMinute.Should().Be(2);
        result.ObligationHydration.RateBackoffReason.Should().BeNull();
        result.ObligationHydration.RecentDownstreamReadLatencyMilliseconds.Should().BeNull();
        result.ObligationHydration.RecentDownstreamReadFailurePercentage.Should().Be(0);
        result.ObligationHydration.EstimatedFullRefreshMinutes.Should().Be(1);
        result.ObligationHydration.EstimatedStalenessGapMinutes.Should().Be(0);
        result.ObligationHydration.Years.Should().ContainSingle();
        var year = result.ObligationHydration.Years.Single(x => x.ObligationYear == 2026);
        year.ObligationYear.Should().Be(2026);
        year.ActiveSummaryCount.Should().Be(2);
        year.DueSummaryCount.Should().Be(1);
        year.PendingSummaryCount.Should().Be(1);
        year.ReadySummaryCount.Should().Be(1);
        year.FailedSummaryCount.Should().Be(0);
        year.NeverSuccessfullyReadSummaryCount.Should().Be(1);
        year.OldestDueAt.Should().Be(utcNow.AddMinutes(-15));
        year.OldestSuccessfulReadAt.Should().Be(utcNow.AddMinutes(-5));
        year.MinimumFullRefreshMinutes.Should().Be(0.01);
        result.ObligationHydration.Lease.IsHeld.Should().BeFalse();
        result.ObligationHydration.Lease.LastReleasedAt.Should().Be(utcNow.AddMinutes(-2));
        result
            .ObligationHydration.HistoricalBackfills.Should()
            .BeEquivalentTo([
                new
                {
                    ObligationYear = 2023,
                    PotentialHydrationOrganisationCount = 1,
                    Status = "Completed",
                    RequestedAt = utcNow.AddMinutes(-30),
                    CompletedAt = (DateTime?)utcNow.AddMinutes(-1),
                    DeferralReason = (string?)"LeaseHeld",
                    DeferredAt = (DateTime?)utcNow.AddMinutes(-2),
                },
                new
                {
                    ObligationYear = 2024,
                    PotentialHydrationOrganisationCount = 2,
                    Status = "Deferred",
                    RequestedAt = utcNow.AddMinutes(-10),
                    CompletedAt = (DateTime?)null,
                    DeferralReason = (string?)"CurrentYearWorkDue",
                    DeferredAt = (DateTime?)utcNow.AddMinutes(-1),
                },
                new
                {
                    ObligationYear = 2025,
                    PotentialHydrationOrganisationCount = 1,
                    Status = "Running",
                    RequestedAt = utcNow.AddMinutes(-20),
                    CompletedAt = (DateTime?)null,
                    DeferralReason = (string?)null,
                    DeferredAt = (DateTime?)null,
                },
            ]);
    }

    [Fact]
    public async Task Get_DuringJanuaryHandover_ShouldCalculateCapacityAcrossBothHydrationYears()
    {
        _timeProvider.SetUtcNow(new DateTimeOffset(2027, 1, 15, 9, 0, 0, TimeSpan.Zero));
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        await OrganisationObligationSummaries.InsertManyAsync(
            [
                Summary(
                    refreshState: OrganisationObligationRefreshState.Ready,
                    nextRefreshAt: utcNow.AddMinutes(15),
                    lastSuccessfulReadAt: utcNow.AddMinutes(-5),
                    obligationYear: 2026
                ),
                Summary(
                    refreshState: OrganisationObligationRefreshState.Ready,
                    nextRefreshAt: utcNow.AddMinutes(15),
                    lastSuccessfulReadAt: utcNow.AddMinutes(-5),
                    obligationYear: 2026
                ),
                Summary(
                    refreshState: OrganisationObligationRefreshState.Pending,
                    nextRefreshAt: utcNow.AddMinutes(-5),
                    lastSuccessfulReadAt: null,
                    obligationYear: 2027
                ),
                Summary(
                    refreshState: OrganisationObligationRefreshState.Pending,
                    nextRefreshAt: utcNow.AddMinutes(-5),
                    lastSuccessfulReadAt: null,
                    obligationYear: 2027
                ),
            ],
            cancellationToken: TestContext.Current.CancellationToken
        );
        var subject = await CreateSubject(activeSummaryCount: 4);

        var result = await subject.Get(TestContext.Current.CancellationToken);

        result.ObligationHydration.TotalMinimumFullRefreshMinutes.Should().Be(0.02);
        result.ObligationHydration.EstimatedFullRefreshMinutes.Should().Be(4);
        result.ObligationHydration.EstimatedStalenessGapMinutes.Should().Be(0);
        result
            .ObligationHydration.Years.Should()
            .BeEquivalentTo([
                new { ObligationYear = 2026, ActiveSummaryCount = 2 },
                new { ObligationYear = 2027, ActiveSummaryCount = 2 },
            ]);
    }

    private async Task<UnsubmittedPollingStatusService> CreateSubject(int activeSummaryCount = 60)
    {
        var dbContext = new MongoDbContext(
            GetMongoApplicationDatabase(),
            Options.Create(new MongoDbOptions()),
            NullLogger<MongoDbContext>.Instance
        );

        var options = Options.Create(
            new OrganisationObligationHydrationOptions { MaxDownstreamRequestsPerMinute = 200 }
        );
        var pacingStateStore = new OrganisationObligationRequestPacingStateStore(
            GetMongoApplicationDatabase(),
            _timeProvider
        );
        var requestPacer = new OrganisationObligationRequestPacer(pacingStateStore, options, _timeProvider);
        await requestPacer.ObserveWorkload(activeSummaryCount, TestContext.Current.CancellationToken);

        return new UnsubmittedPollingStatusService(
            dbContext,
            Options.Create(new OrganisationEligibilityOptions()),
            options,
            new OrganisationObligationHistoricalBackfillStore(GetMongoApplicationDatabase(), _timeProvider),
            pacingStateStore,
            new CurrentObligationYearProvider(_timeProvider),
            new UnsubmittedPollingLeaseStatusService(GetMongoApplicationDatabase(), _timeProvider)
        );
    }

    private static OrganisationComplianceDeclarationEligibility Eligibility(
        string generation,
        OrganisationRegistrationStatus registrationStatus,
        OrganisationReferenceNumberResolutionState referenceNumberResolutionState,
        bool isVisible,
        Guid? organisationId = null,
        RegistrationType registrationType = RegistrationType.DirectProducer,
        int obligationYear = 2026
    ) =>
        new()
        {
            Generation = generation,
            OrganisationId = organisationId ?? Guid.NewGuid(),
            ObligationYear = obligationYear,
            RegistrationType = registrationType,
            RegistrationStatus = registrationStatus,
            ReferenceNumberResolutionState = referenceNumberResolutionState,
            IsVisibleInUnsubmittedView = isVisible,
            Name = "Example organisation",
            SourceFingerprint = "fingerprint",
        };

    private static OrganisationObligationSummary Summary(
        OrganisationObligationRefreshState refreshState,
        DateTime nextRefreshAt,
        DateTime? lastSuccessfulReadAt,
        bool isHydrationActive = true,
        int obligationYear = 2026
    ) =>
        new()
        {
            OrganisationId = Guid.NewGuid(),
            ObligationYear = obligationYear,
            NextRefreshAt = nextRefreshAt,
            LastSuccessfulReadAt = lastSuccessfulReadAt,
            LastAttemptedAt = nextRefreshAt,
            RequestedAt = nextRefreshAt,
            Priority = OrganisationObligationHydrationPriority.ScheduledRefresh,
            IsHydrationActive = isHydrationActive,
            RefreshState = refreshState,
        };
}
