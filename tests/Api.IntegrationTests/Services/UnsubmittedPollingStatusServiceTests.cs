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
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        await OrganisationEligibilitySnapshots.InsertOneAsync(
            new OrganisationEligibilitySnapshot
            {
                Id = OrganisationEligibilitySnapshot.SnapshotId,
                ActiveGeneration = activeGeneration,
                ActiveContentFingerprint = "fingerprint",
                ActiveRowCount = 2,
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
                    isVisible: true
                ),
                Eligibility(
                    activeGeneration,
                    OrganisationRegistrationStatus.Cancelled,
                    OrganisationReferenceNumberResolutionState.Pending,
                    isVisible: false
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
        var subject = CreateSubject();

        var result = await subject.Get(TestContext.Current.CancellationToken);

        result.Eligibility.ActiveGeneration.Should().Be(activeGeneration);
        result.Eligibility.ActiveRowCount.Should().Be(2);
        result.Eligibility.VisibleRowCount.Should().Be(1);
        result.Eligibility.HydrationEligibleOrganisationCount.Should().Be(1);
        result
            .Eligibility.ReferenceResolutionStates.Should()
            .Contain(x => x.State == "Resolved" && x.Count == 1)
            .And.Contain(x => x.State == "Pending" && x.Count == 1);
        result.Eligibility.Lease.IsHeld.Should().BeTrue();
        result.ObligationHydration.MaxDownstreamRequestsPerMinute.Should().Be(200);
        result.ObligationHydration.Years.Should().ContainSingle();
        var year = result.ObligationHydration.Years.Single();
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
    }

    private UnsubmittedPollingStatusService CreateSubject()
    {
        var dbContext = new MongoDbContext(
            GetMongoApplicationDatabase(),
            Options.Create(new MongoDbOptions()),
            NullLogger<MongoDbContext>.Instance
        );

        return new UnsubmittedPollingStatusService(
            dbContext,
            GetMongoApplicationDatabase(),
            Options.Create(new OrganisationEligibilityOptions()),
            Options.Create(new OrganisationObligationHydrationOptions { MaxDownstreamRequestsPerMinute = 200 }),
            _timeProvider
        );
    }

    private static OrganisationComplianceDeclarationEligibility Eligibility(
        string generation,
        OrganisationRegistrationStatus registrationStatus,
        OrganisationReferenceNumberResolutionState referenceNumberResolutionState,
        bool isVisible
    ) =>
        new()
        {
            Generation = generation,
            OrganisationId = Guid.NewGuid(),
            ObligationYear = 2026,
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
        bool isHydrationActive = true
    ) =>
        new()
        {
            OrganisationId = Guid.NewGuid(),
            ObligationYear = 2026,
            NextRefreshAt = nextRefreshAt,
            LastSuccessfulReadAt = lastSuccessfulReadAt,
            LastAttemptedAt = nextRefreshAt,
            RequestedAt = nextRefreshAt,
            Priority = OrganisationObligationHydrationPriority.ScheduledRefresh,
            IsHydrationActive = isHydrationActive,
            RefreshState = refreshState,
        };
}
