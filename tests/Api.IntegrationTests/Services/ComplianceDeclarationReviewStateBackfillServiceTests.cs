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
        reviewState.SubmittedOrAcceptedCount.Should().Be(2);
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

    private ComplianceDeclarationReviewStateBackfillService CreateSubject() =>
        new(
            new MongoDbContext(
                GetMongoDatabase(),
                Options.Create(new MongoDbOptions()),
                Substitute.For<ILogger<MongoDbContext>>()
            ),
            _timeProvider
        );
}
