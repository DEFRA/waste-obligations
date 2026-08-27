using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.OrganisationObligations;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Testing.Fixtures.PrnCommonBackend;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Driver;
using NSubstitute;
using ObligationStatus = Defra.WasteObligations.Api.Dtos.ObligationStatus;
using PrnObligation = Defra.WasteObligations.Api.Services.PrnCommonBackend.Obligation;

namespace Defra.WasteObligations.Api.IntegrationTests.Services.OrganisationObligations;

public class OrganisationObligationHydrationServiceTests : IntegrationTestBase
{
    private const int ObligationYear = 2026;
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero));
    private IOrganisationObligationSource ObligationSource { get; } = Substitute.For<IOrganisationObligationSource>();

    [Fact]
    public async Task EnqueueNewEligible_WhenNoActiveGenerationExists_ShouldNotCreateWork()
    {
        var subject = CreateSubject();

        var enqueuedCount = await subject.EnqueueNewEligible(ObligationYear, TestContext.Current.CancellationToken);

        enqueuedCount.Should().Be(0);
    }

    [Fact]
    public async Task EnqueueNewEligible_ShouldDeduplicateActiveRegisteredResolvedRows()
    {
        var organisationId = Guid.NewGuid();
        await InsertActiveSnapshot();
        await InsertEligibility(organisationId, RegistrationType.DirectProducer);
        await InsertEligibility(organisationId, RegistrationType.ComplianceScheme);
        await InsertEligibility(
            Guid.NewGuid(),
            RegistrationType.DirectProducer,
            registrationStatus: OrganisationRegistrationStatus.Cancelled
        );
        await InsertEligibility(
            Guid.NewGuid(),
            RegistrationType.DirectProducer,
            referenceResolutionState: OrganisationReferenceNumberResolutionState.NotFound
        );
        await InsertEligibility(Guid.NewGuid(), RegistrationType.DirectProducer, obligationYear: ObligationYear - 1);
        var subject = CreateSubject();

        var enqueuedCount = await subject.EnqueueNewEligible(ObligationYear, TestContext.Current.CancellationToken);

        enqueuedCount.Should().Be(1);
        var work = await OrganisationObligationHydrationWork
            .Find(Builders<OrganisationObligationHydrationWork>.Filter.Empty)
            .SingleAsync(TestContext.Current.CancellationToken);
        work.OrganisationId.Should().Be(organisationId);
        work.ObligationYear.Should().Be(ObligationYear);
        work.Priority.Should().Be(OrganisationObligationHydrationPriority.NewEligible);
        work.NextAttemptAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task EnqueueNewEligible_WhenASuccessfulSummaryExists_ShouldNotCreateMoreWork()
    {
        var organisationId = Guid.NewGuid();
        await InsertActiveSnapshot();
        await InsertEligibility(organisationId, RegistrationType.DirectProducer);
        await InsertSummary(
            organisationId,
            OrganisationObligationRefreshState.Ready,
            _timeProvider.GetUtcNow().UtcDateTime
        );
        var subject = CreateSubject();

        var enqueuedCount = await subject.EnqueueNewEligible(ObligationYear, TestContext.Current.CancellationToken);

        enqueuedCount.Should().Be(0);
        (
            await OrganisationObligationHydrationWork.CountDocumentsAsync(
                Builders<OrganisationObligationHydrationWork>.Filter.Empty,
                cancellationToken: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be(0);
    }

    [Theory]
    [InlineData(OrganisationRegistrationStatus.Cancelled, OrganisationReferenceNumberResolutionState.Resolved)]
    [InlineData(OrganisationRegistrationStatus.Registered, OrganisationReferenceNumberResolutionState.NotFound)]
    public async Task HydrateDue_WhenQueuedOrganisationIsNoLongerEligible_ShouldRemoveWorkWithoutCallingSource(
        OrganisationRegistrationStatus registrationStatus,
        OrganisationReferenceNumberResolutionState referenceResolutionState
    )
    {
        var organisationId = Guid.NewGuid();
        await InsertEligibility(organisationId, RegistrationType.DirectProducer, generation: "previous");
        await InsertWork(organisationId);
        await InsertEligibility(
            organisationId,
            RegistrationType.DirectProducer,
            registrationStatus: registrationStatus,
            referenceResolutionState: referenceResolutionState,
            generation: "current"
        );
        await InsertActiveSnapshot("current");
        var subject = CreateSubject();

        var processedCount = await subject.HydrateDue(ObligationYear, TestContext.Current.CancellationToken);

        processedCount.Should().Be(0);
        await ObligationSource
            .DidNotReceive()
            .ReadObligations(organisationId, ObligationYear, Arg.Any<CancellationToken>());
        (
            await OrganisationObligationHydrationWork.CountDocumentsAsync(
                Builders<OrganisationObligationHydrationWork>.Filter.Empty,
                cancellationToken: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be(0);
    }

    [Fact]
    public async Task EnqueueReconciliation_ShouldMakePreCutoverScheduledWorkDue()
    {
        var organisationId = Guid.NewGuid();
        var readBeforeCutover = _timeProvider.GetUtcNow().AddMinutes(-2).UtcDateTime;
        var cutover = _timeProvider.GetUtcNow().AddMinutes(-1).UtcDateTime;
        var readAfterCutoverOrganisationId = Guid.NewGuid();
        await InsertActiveSnapshot();
        await InsertEligibility(organisationId, RegistrationType.DirectProducer);
        await InsertEligibility(readAfterCutoverOrganisationId, RegistrationType.DirectProducer);
        await InsertWork(
            organisationId,
            nextAttemptAt: _timeProvider.GetUtcNow().AddHours(1).UtcDateTime,
            lastSuccessfulReadAt: readBeforeCutover
        );
        await InsertWork(
            readAfterCutoverOrganisationId,
            nextAttemptAt: _timeProvider.GetUtcNow().AddHours(1).UtcDateTime,
            lastSuccessfulReadAt: _timeProvider.GetUtcNow().UtcDateTime
        );
        var subject = CreateSubject();

        var enqueuedCount = await subject.EnqueueReconciliation(
            ObligationYear,
            cutover,
            TestContext.Current.CancellationToken
        );

        enqueuedCount.Should().Be(1);
        var reconciledWork = await OrganisationObligationHydrationWork
            .Find(x => x.OrganisationId == organisationId)
            .SingleAsync(TestContext.Current.CancellationToken);
        reconciledWork.Priority.Should().Be(OrganisationObligationHydrationPriority.Reconciliation);
        reconciledWork.NextAttemptAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        var recentWork = await OrganisationObligationHydrationWork
            .Find(x => x.OrganisationId == readAfterCutoverOrganisationId)
            .SingleAsync(TestContext.Current.CancellationToken);
        recentWork.Priority.Should().Be(OrganisationObligationHydrationPriority.ScheduledRefresh);
        recentWork.NextAttemptAt.Should().Be(_timeProvider.GetUtcNow().AddHours(1).UtcDateTime);
    }

    [Fact]
    public async Task HydrateDue_WhenSourceSucceeds_ShouldPersistReadySummaryAndScheduleRefresh()
    {
        var organisationId = Guid.NewGuid();
        await InsertActiveSnapshot();
        await InsertEligibility(organisationId, RegistrationType.DirectProducer);
        ObligationSource
            .ReadObligations(organisationId, ObligationYear, Arg.Any<CancellationToken>())
            .Returns([
                CreateObligation("Glass", accepted: 15, obligated: 20, ObligationStatus.Met),
                CreateObligation("Plastic", accepted: 20, obligated: 20, ObligationStatus.NotMet),
            ]);
        var subject = CreateSubject();

        var processedCount = await subject.HydrateDue(ObligationYear, TestContext.Current.CancellationToken);

        processedCount.Should().Be(1);
        var summary = await OrganisationObligationSummaries
            .Find(x => x.OrganisationId == organisationId && x.ObligationYear == ObligationYear)
            .SingleAsync(TestContext.Current.CancellationToken);
        summary.ObligationCount.Should().Be(2);
        summary.TotalAcceptedTonnage.Should().Be(35);
        summary.TotalObligatedTonnage.Should().Be(40);
        summary.RecyclingObligationsMet.Should().BeFalse();
        summary.ObligationCoveragePercentage.Should().Be(88);
        summary.RefreshState.Should().Be(OrganisationObligationRefreshState.Ready);
        summary.LastSuccessfulReadAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        summary.NextRefreshAt.Should().BeAfter(_timeProvider.GetUtcNow().UtcDateTime);
        var work = await OrganisationObligationHydrationWork
            .Find(x => x.OrganisationId == organisationId && x.ObligationYear == ObligationYear)
            .SingleAsync(TestContext.Current.CancellationToken);
        work.Priority.Should().Be(OrganisationObligationHydrationPriority.ScheduledRefresh);
        work.NextAttemptAt.Should().Be(summary.NextRefreshAt);
        await ObligationSource
            .Received(1)
            .ReadObligations(organisationId, ObligationYear, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HydrateDue_WhenSourceReturnsNoObligations_ShouldPersistReadyEmptySummary()
    {
        var organisationId = Guid.NewGuid();
        await InsertActiveSnapshot();
        await InsertEligibility(organisationId, RegistrationType.DirectProducer);
        ObligationSource.ReadObligations(organisationId, ObligationYear, Arg.Any<CancellationToken>()).Returns([]);
        var subject = CreateSubject();

        await subject.HydrateDue(ObligationYear, TestContext.Current.CancellationToken);

        var summary = await OrganisationObligationSummaries
            .Find(x => x.OrganisationId == organisationId && x.ObligationYear == ObligationYear)
            .SingleAsync(TestContext.Current.CancellationToken);
        summary.ObligationCount.Should().Be(0);
        summary.RecyclingObligationsMet.Should().BeNull();
        summary.ObligationCoveragePercentage.Should().Be(0);
        summary.RefreshState.Should().Be(OrganisationObligationRefreshState.Ready);
    }

    [Fact]
    public async Task HydrateDue_WhenSourceFails_ShouldRetainFailureAndScheduleRetry()
    {
        var organisationId = Guid.NewGuid();
        await InsertActiveSnapshot();
        await InsertEligibility(organisationId, RegistrationType.DirectProducer);
        ObligationSource
            .ReadObligations(organisationId, ObligationYear, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IEnumerable<PrnObligation>>(new HttpRequestException("PRN is unavailable")));
        var subject = CreateSubject();

        var processedCount = await subject.HydrateDue(ObligationYear, TestContext.Current.CancellationToken);

        processedCount.Should().Be(1);
        var summary = await OrganisationObligationSummaries
            .Find(x => x.OrganisationId == organisationId && x.ObligationYear == ObligationYear)
            .SingleAsync(TestContext.Current.CancellationToken);
        summary.RefreshState.Should().Be(OrganisationObligationRefreshState.Failed);
        summary.LastSuccessfulReadAt.Should().BeNull();
        summary.LastFailure.Should().Be("PRN is unavailable");
        var work = await OrganisationObligationHydrationWork
            .Find(x => x.OrganisationId == organisationId && x.ObligationYear == ObligationYear)
            .SingleAsync(TestContext.Current.CancellationToken);
        work.Priority.Should().Be(OrganisationObligationHydrationPriority.Retry);
        work.AttemptCount.Should().Be(1);
        work.NextAttemptAt.Should().Be(_timeProvider.GetUtcNow().AddMinutes(1).UtcDateTime);
    }

    [Fact]
    public async Task HydrateDue_WhenRefreshFailsAfterAPreviousSuccess_ShouldRetainTheLastMetrics()
    {
        var organisationId = Guid.NewGuid();
        var successfulReadAt = _timeProvider.GetUtcNow().AddMinutes(-30).UtcDateTime;
        await InsertActiveSnapshot();
        await InsertEligibility(organisationId, RegistrationType.DirectProducer);
        await InsertSummary(organisationId, OrganisationObligationRefreshState.Ready, successfulReadAt);
        await InsertWork(organisationId);
        ObligationSource
            .ReadObligations(organisationId, ObligationYear, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IEnumerable<PrnObligation>>(new HttpRequestException("PRN is unavailable")));
        var subject = CreateSubject();

        await subject.HydrateDue(ObligationYear, TestContext.Current.CancellationToken);

        var summary = await OrganisationObligationSummaries
            .Find(x => x.OrganisationId == organisationId && x.ObligationYear == ObligationYear)
            .SingleAsync(TestContext.Current.CancellationToken);
        summary.RefreshState.Should().Be(OrganisationObligationRefreshState.Failed);
        summary.LastSuccessfulReadAt.Should().Be(successfulReadAt);
        summary.TotalAcceptedTonnage.Should().Be(4);
        summary.TotalObligatedTonnage.Should().Be(5);
        summary.ObligationCoveragePercentage.Should().Be(80);
    }

    private OrganisationObligationHydrationService CreateSubject()
    {
        var database = GetMongoDatabase();
        var dbContext = new MongoDbContext(
            database,
            Options.Create(new MongoDbOptions()),
            Substitute.For<Microsoft.Extensions.Logging.ILogger<MongoDbContext>>()
        );

        return new OrganisationObligationHydrationService(
            dbContext,
            ObligationSource,
            new OrganisationObligationRequestPacer(
                Options.Create(new OrganisationObligationHydrationOptions()),
                _timeProvider
            ),
            Options.Create(new OrganisationObligationHydrationOptions()),
            _timeProvider
        );
    }

    private Task InsertActiveSnapshot(string activeGeneration = "active") =>
        OrganisationEligibilitySnapshots.InsertOneAsync(
            new OrganisationEligibilitySnapshot
            {
                Id = OrganisationEligibilitySnapshot.SnapshotId,
                ActiveGeneration = activeGeneration,
                ActiveContentFingerprint = "fingerprint",
                ActiveRowCount = 1,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );

    private Task InsertEligibility(
        Guid organisationId,
        RegistrationType registrationType,
        int obligationYear = ObligationYear,
        OrganisationRegistrationStatus registrationStatus = OrganisationRegistrationStatus.Registered,
        OrganisationReferenceNumberResolutionState referenceResolutionState =
            OrganisationReferenceNumberResolutionState.Resolved,
        string generation = "active"
    ) =>
        OrganisationComplianceDeclarationEligibilities.InsertOneAsync(
            new OrganisationComplianceDeclarationEligibility
            {
                Generation = generation,
                OrganisationId = organisationId,
                ObligationYear = obligationYear,
                RegistrationType = registrationType,
                RegistrationStatus = registrationStatus,
                Name = "Organisation",
                ReferenceNumber = "reference",
                ReferenceNumberResolutionState = referenceResolutionState,
                SourceFingerprint = "fingerprint",
                RefreshedAt = _timeProvider.GetUtcNow().UtcDateTime,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );

    private Task InsertSummary(
        Guid organisationId,
        OrganisationObligationRefreshState refreshState,
        DateTime? lastSuccessfulReadAt
    ) =>
        OrganisationObligationSummaries.InsertOneAsync(
            new OrganisationObligationSummary
            {
                OrganisationId = organisationId,
                ObligationYear = ObligationYear,
                ObligationCount = 1,
                TotalAcceptedTonnage = 4,
                TotalObligatedTonnage = 5,
                RecyclingObligationsMet = true,
                ObligationCoveragePercentage = 80,
                SourceFingerprint = "summary-fingerprint",
                LastSuccessfulReadAt = lastSuccessfulReadAt,
                LastAttemptedAt = _timeProvider.GetUtcNow().UtcDateTime,
                NextRefreshAt = _timeProvider.GetUtcNow().UtcDateTime,
                RefreshState = refreshState,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );

    private Task InsertWork(
        Guid organisationId,
        DateTime? nextAttemptAt = null,
        DateTime? lastSuccessfulReadAt = null
    ) =>
        OrganisationObligationHydrationWork.InsertOneAsync(
            new OrganisationObligationHydrationWork
            {
                OrganisationId = organisationId,
                ObligationYear = ObligationYear,
                Priority = OrganisationObligationHydrationPriority.ScheduledRefresh,
                NextAttemptAt = nextAttemptAt ?? _timeProvider.GetUtcNow().UtcDateTime,
                RequestedAt = _timeProvider.GetUtcNow().UtcDateTime,
                LastSuccessfulReadAt = lastSuccessfulReadAt,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );

    private static PrnObligation CreateObligation(string materialName, int accepted, int obligated, string status) =>
        ObligationFixture
            .Default()
            .With(x => x.MaterialName, materialName)
            .With(x => x.TonnageAccepted, accepted)
            .With(x => x.ObligationToMeet, obligated)
            .With(x => x.Status, status)
            .Create();
}
