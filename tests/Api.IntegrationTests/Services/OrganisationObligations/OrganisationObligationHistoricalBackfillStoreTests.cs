using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.OrganisationObligations;
using Microsoft.Extensions.Time.Testing;

namespace Defra.WasteObligations.Api.IntegrationTests.Services.OrganisationObligations;

public class OrganisationObligationHistoricalBackfillStoreTests : IntegrationTestBase
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 9, 8, 16, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task CreateGetAndComplete_ShouldPersistOneResumableBackfillPerYear()
    {
        var subject = new OrganisationObligationHistoricalBackfillStore(GetMongoApplicationDatabase(), _timeProvider);
        var firstBackfill = Backfill(2024, _timeProvider.GetUtcNow().AddMinutes(-1).UtcDateTime);
        var secondBackfill = Backfill(2025, _timeProvider.GetUtcNow().UtcDateTime);

        var firstCreation = await subject.Create(firstBackfill, TestContext.Current.CancellationToken);
        var secondCreation = await subject.Create(secondBackfill, TestContext.Current.CancellationToken);
        var existingCreation = await subject.Create(
            Backfill(2024, _timeProvider.GetUtcNow().UtcDateTime),
            TestContext.Current.CancellationToken
        );

        firstCreation.WasCreated.Should().BeTrue();
        secondCreation.WasCreated.Should().BeTrue();
        existingCreation.WasCreated.Should().BeFalse();
        existingCreation.Backfill.Should().BeEquivalentTo(firstBackfill);
        (await subject.GetNextIncomplete(TestContext.Current.CancellationToken)).Should().BeEquivalentTo(firstBackfill);

        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        await subject.Complete(firstBackfill, TestContext.Current.CancellationToken);

        (await subject.GetNextIncomplete(TestContext.Current.CancellationToken))
            .Should()
            .BeEquivalentTo(secondBackfill);
        var all = await subject.GetAll(TestContext.Current.CancellationToken);
        all.Should()
            .BeEquivalentTo(
                [
                    firstBackfill with
                    {
                        UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime,
                        CompletedAt = _timeProvider.GetUtcNow().UtcDateTime,
                    },
                    secondBackfill,
                ],
                options => options.WithStrictOrdering()
            );
    }

    [Fact]
    public async Task MarkEnqueued_ShouldRecordThatTheBackfillPopulationHasBeenQueued()
    {
        var subject = new OrganisationObligationHistoricalBackfillStore(GetMongoApplicationDatabase(), _timeProvider);
        var backfill = Backfill(2025, _timeProvider.GetUtcNow().UtcDateTime);
        await subject.Create(backfill, TestContext.Current.CancellationToken);
        _timeProvider.Advance(TimeSpan.FromMinutes(1));

        await subject.MarkEnqueued(backfill, TestContext.Current.CancellationToken);

        (await subject.GetNextIncomplete(TestContext.Current.CancellationToken))
            .Should()
            .BeEquivalentTo(
                backfill with
                {
                    EnqueuedAt = _timeProvider.GetUtcNow().UtcDateTime,
                    UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime,
                }
            );
    }

    [Fact]
    public async Task MarkDeferredAndClearDeferral_ShouldRecordTheCurrentDeferralState()
    {
        var subject = new OrganisationObligationHistoricalBackfillStore(GetMongoApplicationDatabase(), _timeProvider);
        var backfill = Backfill(2025, _timeProvider.GetUtcNow().UtcDateTime);
        await subject.Create(backfill, TestContext.Current.CancellationToken);
        _timeProvider.Advance(TimeSpan.FromMinutes(1));

        await subject.MarkDeferred(
            backfill,
            OrganisationObligationHistoricalBackfillDeferralReason.CurrentYearWorkDue,
            TestContext.Current.CancellationToken
        );

        (await subject.GetNextIncomplete(TestContext.Current.CancellationToken))
            .Should()
            .BeEquivalentTo(
                backfill with
                {
                    DeferralReason = OrganisationObligationHistoricalBackfillDeferralReason.CurrentYearWorkDue,
                    DeferredAt = _timeProvider.GetUtcNow().UtcDateTime,
                    UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime,
                }
            );
        _timeProvider.Advance(TimeSpan.FromMinutes(1));

        await subject.ClearDeferral(backfill, TestContext.Current.CancellationToken);

        (await subject.GetNextIncomplete(TestContext.Current.CancellationToken))
            .Should()
            .BeEquivalentTo(backfill with { UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime });
    }

    [Fact]
    public async Task Create_WhenRequestsForTheSameYearRace_ShouldReturnTheSingleCreatedBackfill()
    {
        var subject = new OrganisationObligationHistoricalBackfillStore(GetMongoApplicationDatabase(), _timeProvider);
        var requestedAt = _timeProvider.GetUtcNow().UtcDateTime;
        var backfills = Enumerable.Range(0, 20).Select(_ => Backfill(2025, requestedAt)).ToArray();

        var creations = await Task.WhenAll(
            backfills.Select(backfill => subject.Create(backfill, TestContext.Current.CancellationToken))
        );

        creations.Count(x => x.WasCreated).Should().Be(1);
        creations.Select(x => x.Backfill).Should().AllBeEquivalentTo(creations.Single(x => x.WasCreated).Backfill);
    }

    private static OrganisationObligationHistoricalBackfill Backfill(int obligationYear, DateTime requestedAt) =>
        new()
        {
            ObligationYear = obligationYear,
            OrganisationIds = [Guid.NewGuid()],
            RequestedAt = requestedAt,
            UpdatedAt = requestedAt,
        };
}
