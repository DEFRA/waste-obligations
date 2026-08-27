using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Driver;
using NSubstitute;

namespace Defra.WasteObligations.Api.IntegrationTests.Services;

public class ComplianceDeclarationReviewStateBackfillServiceTests : IntegrationTestBase
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Backfill_WhenIncomplete_ShouldWriteSubmittedAndAcceptedCountsThenMarkComplete()
    {
        var organisation = OrganisationFixture.DirectProducer().Create();
        await ComplianceDeclarations.InsertManyAsync(
            [
                ComplianceDeclarationFixture
                    .Default()
                    .With(x => x.Organisation, organisation)
                    .With(x => x.Status, ComplianceDeclarationStatus.Submitted)
                    .Create(),
                ComplianceDeclarationFixture
                    .Default()
                    .With(x => x.Organisation, organisation)
                    .With(x => x.Status, ComplianceDeclarationStatus.Accepted)
                    .Create(),
                ComplianceDeclarationFixture
                    .Default()
                    .With(x => x.Organisation, organisation)
                    .With(x => x.Status, ComplianceDeclarationStatus.Cancelled)
                    .Create(),
            ],
            cancellationToken: TestContext.Current.CancellationToken
        );
        var subject = CreateSubject();

        var result = await subject.Backfill(TestContext.Current.CancellationToken);

        result.AlreadyComplete.Should().BeFalse();
        result.StateRowCount.Should().Be(1);
        var reviewState = await ComplianceDeclarationReviewStates
            .Find(x =>
                x.OrganisationId == organisation.Id
                && x.ObligationYear == 2026
                && x.RegistrationType == organisation.RegistrationType
            )
            .SingleAsync(TestContext.Current.CancellationToken);
        reviewState.UnsubmittedExclusionCount.Should().Be(2);
        reviewState.UpdatedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        var snapshot = await ComplianceDeclarationReviewStateSnapshots
            .Find(x => x.Id == ComplianceDeclarationReviewStateSnapshot.SnapshotId)
            .SingleAsync(TestContext.Current.CancellationToken);
        snapshot.BackfillCompletedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task Backfill_WhenAlreadyComplete_ShouldNotRebuildState()
    {
        await ComplianceDeclarationReviewStateSnapshots.InsertOneAsync(
            new ComplianceDeclarationReviewStateSnapshot
            {
                Id = ComplianceDeclarationReviewStateSnapshot.SnapshotId,
                BackfillCompletedAt = _timeProvider.GetUtcNow().UtcDateTime,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        var subject = CreateSubject();

        var result = await subject.Backfill(TestContext.Current.CancellationToken);

        result.AlreadyComplete.Should().BeTrue();
        result.StateRowCount.Should().Be(0);
        (
            await ComplianceDeclarationReviewStates.CountDocumentsAsync(
                FilterDefinition<ComplianceDeclarationReviewState>.Empty,
                cancellationToken: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be(0);
    }

    [Fact]
    public async Task Backfill_WhenExistingStateNoLongerHasAnExclusion_ShouldResetCount()
    {
        var organisation = OrganisationFixture.DirectProducer().Create();
        await ComplianceDeclarationReviewStates.InsertOneAsync(
            new ComplianceDeclarationReviewState
            {
                OrganisationId = organisation.Id,
                ObligationYear = 2026,
                RegistrationType = organisation.RegistrationType,
                UnsubmittedExclusionCount = 1,
                UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-1),
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        var subject = CreateSubject();

        var result = await subject.Backfill(TestContext.Current.CancellationToken);

        result.AlreadyComplete.Should().BeFalse();
        result.StateRowCount.Should().Be(1);
        var reviewState = await ComplianceDeclarationReviewStates
            .Find(x => x.OrganisationId == organisation.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
        reviewState.UnsubmittedExclusionCount.Should().Be(0);
        reviewState.UpdatedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task ReconcileInitialRollout_ShouldAddMissingCountsAndResetObsoleteCountsThenMarkComplete()
    {
        var submittedOrganisation = OrganisationFixture.DirectProducer().Create();
        var cancelledOrganisation = OrganisationFixture.ComplianceScheme().Create();
        await ComplianceDeclarations.InsertOneAsync(
            ComplianceDeclarationFixture
                .Default()
                .With(x => x.Organisation, submittedOrganisation)
                .With(x => x.Status, ComplianceDeclarationStatus.Submitted)
                .Create(),
            cancellationToken: TestContext.Current.CancellationToken
        );
        await ComplianceDeclarationReviewStates.InsertOneAsync(
            new ComplianceDeclarationReviewState
            {
                OrganisationId = cancelledOrganisation.Id,
                ObligationYear = 2026,
                RegistrationType = cancelledOrganisation.RegistrationType,
                UnsubmittedExclusionCount = 1,
                UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-1),
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        var backfillCompletedAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-10);
        await ComplianceDeclarationReviewStateSnapshots.InsertOneAsync(
            new ComplianceDeclarationReviewStateSnapshot
            {
                Id = ComplianceDeclarationReviewStateSnapshot.SnapshotId,
                BackfillCompletedAt = backfillCompletedAt,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        var subject = CreateSubject();

        var result = await subject.ReconcileInitialRollout(TestContext.Current.CancellationToken);

        result.AlreadyComplete.Should().BeFalse();
        result.StateRowCount.Should().Be(2);
        var reviewStates = await ComplianceDeclarationReviewStates
            .Find(FilterDefinition<ComplianceDeclarationReviewState>.Empty)
            .ToListAsync(TestContext.Current.CancellationToken);
        reviewStates.Single(x => x.OrganisationId == submittedOrganisation.Id).UnsubmittedExclusionCount.Should().Be(1);
        reviewStates.Single(x => x.OrganisationId == cancelledOrganisation.Id).UnsubmittedExclusionCount.Should().Be(0);
        var snapshot = await ComplianceDeclarationReviewStateSnapshots
            .Find(x => x.Id == ComplianceDeclarationReviewStateSnapshot.SnapshotId)
            .SingleAsync(TestContext.Current.CancellationToken);
        snapshot.BackfillCompletedAt.Should().Be(backfillCompletedAt);
        snapshot.InitialRolloutReconciliationCompletedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task ReconcileInitialRollout_WhenAlreadyComplete_ShouldNotRebuildState()
    {
        var completedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await ComplianceDeclarationReviewStateSnapshots.InsertOneAsync(
            new ComplianceDeclarationReviewStateSnapshot
            {
                Id = ComplianceDeclarationReviewStateSnapshot.SnapshotId,
                BackfillCompletedAt = completedAt,
                InitialRolloutReconciliationCompletedAt = completedAt,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        var subject = CreateSubject();

        var result = await subject.ReconcileInitialRollout(TestContext.Current.CancellationToken);

        result.AlreadyComplete.Should().BeTrue();
        result.StateRowCount.Should().Be(0);
    }

    private ComplianceDeclarationReviewStateBackfillService CreateSubject()
    {
        var logger = Substitute.For<ILogger<MongoDbContext>>();
        var dbContext = new MongoDbContext(GetMongoDatabase(), Options.Create(new MongoDbOptions()), logger);

        return new ComplianceDeclarationReviewStateBackfillService(dbContext, _timeProvider);
    }
}
