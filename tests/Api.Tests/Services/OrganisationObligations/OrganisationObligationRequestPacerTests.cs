using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.OrganisationObligations;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Defra.WasteObligations.Api.Tests.Services.OrganisationObligations;

public class OrganisationObligationRequestPacerTests
{
    [Fact]
    public async Task Wait_ShouldEvenlySpaceRequestsAtTheConfiguredRate()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero));
        var subject = CreateSubject(new PacingStateStore(), timeProvider);
        await subject.ObserveWorkload(600, TestContext.Current.CancellationToken);

        await subject.Wait(TestContext.Current.CancellationToken);
        var secondRequest = subject.Wait(TestContext.Current.CancellationToken);

        secondRequest.IsCompleted.Should().BeFalse();
        timeProvider.Advance(TimeSpan.FromSeconds(3));
        await secondRequest;
    }

    [Fact]
    public async Task Wait_WhenPacerIsRecreated_ShouldKeepTheSharedRequestSchedule()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero));
        var pacingStateStore = new PacingStateStore();
        var firstPacer = CreateSubject(pacingStateStore, timeProvider);
        await firstPacer.ObserveWorkload(600, TestContext.Current.CancellationToken);
        await firstPacer.Wait(TestContext.Current.CancellationToken);
        var secondPacer = CreateSubject(pacingStateStore, timeProvider);

        var secondRequest = secondPacer.Wait(TestContext.Current.CancellationToken);

        secondRequest.IsCompleted.Should().BeFalse();
        timeProvider.Advance(TimeSpan.FromSeconds(3));
        await secondRequest;
    }

    [Fact]
    public async Task ObserveWorkload_WhenPacerIsRecreated_ShouldKeepTheSharedBackoff()
    {
        var pacingStateStore = new PacingStateStore();
        var firstPacer = CreateSubject(pacingStateStore);
        await firstPacer.ObserveWorkload(600, TestContext.Current.CancellationToken);
        await firstPacer.ObserveRead(
            TimeSpan.FromMilliseconds(100),
            succeeded: false,
            TestContext.Current.CancellationToken
        );
        var secondPacer = CreateSubject(pacingStateStore);

        await secondPacer.ObserveWorkload(600, TestContext.Current.CancellationToken);

        (await secondPacer.GetStatus(TestContext.Current.CancellationToken)).EffectiveRequestsPerMinute.Should().Be(16);
    }

    [Fact]
    public async Task ObserveWorkload_ShouldDeriveTheTargetRateAndApplyTheSafetyCeiling()
    {
        var subject = CreateSubject(new PacingStateStore());

        await subject.ObserveWorkload(7230, TestContext.Current.CancellationToken);

        (await subject.GetStatus(TestContext.Current.CancellationToken))
            .Should()
            .BeEquivalentTo(
                new
                {
                    DesiredRequestsPerMinute = 241,
                    EffectiveRequestsPerMinute = 20,
                    BackoffReason = (string?)null,
                    RecentDownstreamLatencyMilliseconds = (double?)null,
                    RecentDownstreamFailurePercentage = 0d,
                }
            );
    }

    [Fact]
    public async Task ObserveWorkload_WhenSummariesAreDue_ShouldIncreaseTheTargetRateToCatchUp()
    {
        var subject = CreateSubject(
            new PacingStateStore(),
            hydrationOptions: new OrganisationObligationHydrationOptions { MaxDownstreamRequestsPerMinute = 180 }
        );

        await subject.ObserveWorkload(1971, 646, TestContext.Current.CancellationToken);

        (await subject.GetStatus(TestContext.Current.CancellationToken))
            .Should()
            .BeEquivalentTo(
                new
                {
                    DesiredRequestsPerMinute = 88,
                    EffectiveRequestsPerMinute = 88,
                    BackoffReason = (string?)null,
                    RecentDownstreamLatencyMilliseconds = (double?)null,
                    RecentDownstreamFailurePercentage = 0d,
                }
            );
    }

    [Fact]
    public async Task ObserveWorkload_WhenTheCatchUpTargetExceedsTheSafetyCeiling_ShouldApplyTheSafetyCeiling()
    {
        var subject = CreateSubject(new PacingStateStore());

        await subject.ObserveWorkload(600, 600, TestContext.Current.CancellationToken);

        (await subject.GetStatus(TestContext.Current.CancellationToken))
            .Should()
            .BeEquivalentTo(
                new
                {
                    DesiredRequestsPerMinute = 40,
                    EffectiveRequestsPerMinute = 20,
                    BackoffReason = (string?)null,
                    RecentDownstreamLatencyMilliseconds = (double?)null,
                    RecentDownstreamFailurePercentage = 0d,
                }
            );
    }

    [Fact]
    public async Task ObserveWorkload_WhenThereIsNoPacingStateAndNoActiveWork_ShouldNotPersistState()
    {
        var pacingStateStore = new PacingStateStore();
        var subject = CreateSubject(pacingStateStore);

        await subject.ObserveWorkload(0, TestContext.Current.CancellationToken);

        pacingStateStore.GetCount.Should().Be(1);
        pacingStateStore.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task ObserveWorkload_WhenActiveWorkBecomesIdle_ShouldResetTheExistingPacingState()
    {
        var pacingStateStore = new PacingStateStore();
        var subject = CreateSubject(pacingStateStore);
        await subject.ObserveWorkload(600, TestContext.Current.CancellationToken);

        await subject.ObserveWorkload(0, TestContext.Current.CancellationToken);

        pacingStateStore.UpdateCount.Should().Be(2);
        (await subject.GetStatus(TestContext.Current.CancellationToken)).EffectiveRequestsPerMinute.Should().Be(0);
    }

    [Fact]
    public async Task ObserveRead_WhenDownstreamLatencyIncreasesButCapacitySustainsTarget_ShouldKeepTargetRate()
    {
        var subject = CreateSubject(
            new PacingStateStore(),
            hydrationOptions: new OrganisationObligationHydrationOptions { MaxDownstreamRequestsPerMinute = 180 }
        );
        await subject.ObserveWorkload(1971, TestContext.Current.CancellationToken);
        for (var index = 0; index < 5; index++)
            await subject.ObserveRead(
                TimeSpan.FromMilliseconds(10.9),
                succeeded: true,
                TestContext.Current.CancellationToken
            );

        for (var index = 0; index < 5; index++)
            await subject.ObserveRead(
                TimeSpan.FromMilliseconds(25.5),
                succeeded: true,
                TestContext.Current.CancellationToken
            );

        var status = await subject.GetStatus(TestContext.Current.CancellationToken);
        status.DesiredRequestsPerMinute.Should().Be(66);
        status.EffectiveRequestsPerMinute.Should().Be(66);
        status.BackoffReason.Should().BeNull();
    }

    [Fact]
    public async Task ObserveRead_WhenLatencyExceedsConcurrentCapacity_ShouldLimitTheControllerRate()
    {
        var subject = CreateSubject(
            new PacingStateStore(),
            hydrationOptions: new OrganisationObligationHydrationOptions
            {
                MaxConcurrentRequests = 2,
                MaxDownstreamRequestsPerMinute = 20,
            }
        );
        await subject.ObserveWorkload(600, TestContext.Current.CancellationToken);

        await subject.ObserveRead(TimeSpan.FromSeconds(20), succeeded: true, TestContext.Current.CancellationToken);

        (await subject.GetStatus(TestContext.Current.CancellationToken)).EffectiveRequestsPerMinute.Should().Be(6);
    }

    [Fact]
    public async Task ObserveRead_WhenDownstreamReadFails_ShouldBackOffTheControllerRate()
    {
        var subject = CreateSubject(new PacingStateStore());
        await subject.ObserveWorkload(600, TestContext.Current.CancellationToken);

        await subject.ObserveRead(
            TimeSpan.FromMilliseconds(100),
            succeeded: false,
            TestContext.Current.CancellationToken
        );

        var status = await subject.GetStatus(TestContext.Current.CancellationToken);
        status.EffectiveRequestsPerMinute.Should().Be(16);
        status.BackoffReason.Should().Be("Downstream read failure rate is 100%");
        status.RecentDownstreamLatencyMilliseconds.Should().BeNull();
        status.RecentDownstreamFailurePercentage.Should().Be(100);
    }

    [Fact]
    public async Task ObserveRead_WhenFailureRemainsInTheRollingWindow_ShouldNotCompoundTheSameBackoff()
    {
        var subject = CreateSubject(new PacingStateStore());
        await subject.ObserveWorkload(600, TestContext.Current.CancellationToken);
        await subject.ObserveRead(
            TimeSpan.FromMilliseconds(100),
            succeeded: false,
            TestContext.Current.CancellationToken
        );

        await subject.ObserveRead(
            TimeSpan.FromMilliseconds(100),
            succeeded: true,
            TestContext.Current.CancellationToken
        );

        (await subject.GetStatus(TestContext.Current.CancellationToken)).EffectiveRequestsPerMinute.Should().Be(16);
    }

    [Fact]
    public async Task ObserveRead_WhenDownstreamFailuresContinue_ShouldCompoundTheBackoff()
    {
        var subject = CreateSubject(new PacingStateStore());
        await subject.ObserveWorkload(600, TestContext.Current.CancellationToken);

        await subject.ObserveRead(
            TimeSpan.FromMilliseconds(100),
            succeeded: false,
            TestContext.Current.CancellationToken
        );
        await subject.ObserveRead(
            TimeSpan.FromMilliseconds(100),
            succeeded: false,
            TestContext.Current.CancellationToken
        );
        await subject.ObserveRead(
            TimeSpan.FromMilliseconds(100),
            succeeded: false,
            TestContext.Current.CancellationToken
        );

        (await subject.GetStatus(TestContext.Current.CancellationToken)).EffectiveRequestsPerMinute.Should().Be(11);
    }

    [Fact]
    public async Task ObserveRead_WhenHistoricalLatencyIsLower_ShouldNotReduceTheTargetRate()
    {
        var subject = CreateSubject(new PacingStateStore());
        await subject.ObserveWorkload(600, TestContext.Current.CancellationToken);
        for (var index = 0; index < 5; index++)
            await subject.ObserveRead(
                TimeSpan.FromMilliseconds(300),
                succeeded: true,
                TestContext.Current.CancellationToken
            );

        for (var index = 0; index < 10; index++)
            await subject.ObserveRead(
                TimeSpan.FromMilliseconds(100),
                succeeded: true,
                TestContext.Current.CancellationToken
            );

        for (var index = 0; index < 5; index++)
            await subject.ObserveRead(
                TimeSpan.FromMilliseconds(300),
                succeeded: true,
                TestContext.Current.CancellationToken
            );

        var status = await subject.GetStatus(TestContext.Current.CancellationToken);
        status.EffectiveRequestsPerMinute.Should().Be(20);
        status.BackoffReason.Should().BeNull();
    }

    [Fact]
    public async Task ObserveRead_WhenFailureRateRecovers_ShouldRestoreTheTargetRate()
    {
        var subject = CreateSubject(new PacingStateStore());
        await subject.ObserveWorkload(600, TestContext.Current.CancellationToken);
        await subject.ObserveRead(
            TimeSpan.FromMilliseconds(100),
            succeeded: false,
            TestContext.Current.CancellationToken
        );

        for (var index = 0; index < 10; index++)
            await subject.ObserveRead(
                TimeSpan.FromMilliseconds(100),
                succeeded: true,
                TestContext.Current.CancellationToken
            );

        var status = await subject.GetStatus(TestContext.Current.CancellationToken);
        status.EffectiveRequestsPerMinute.Should().Be(20);
        status.BackoffReason.Should().BeNull();
    }

    private static OrganisationObligationRequestPacer CreateSubject(
        IOrganisationObligationRequestPacingStateStore pacingStateStore,
        TimeProvider? timeProvider = null,
        OrganisationObligationHydrationOptions? hydrationOptions = null
    ) =>
        new(
            pacingStateStore,
            Options.Create(
                hydrationOptions ?? new OrganisationObligationHydrationOptions { MaxDownstreamRequestsPerMinute = 20 }
            ),
            timeProvider ?? TimeProvider.System
        );

    private sealed class PacingStateStore : IOrganisationObligationRequestPacingStateStore
    {
        private OrganisationObligationRequestPacingState? _state;

        public int GetCount { get; private set; }

        public int UpdateCount { get; private set; }

        public Task<OrganisationObligationRequestPacingState?> Get(CancellationToken cancellationToken)
        {
            GetCount++;

            return Task.FromResult(_state);
        }

        public Task<OrganisationObligationRequestPacingState> Update(
            Func<OrganisationObligationRequestPacingState, OrganisationObligationRequestPacingState> update,
            CancellationToken cancellationToken
        )
        {
            UpdateCount++;
            _state = update(
                _state
                    ?? new OrganisationObligationRequestPacingState
                    {
                        Id = OrganisationObligationRequestPacingState.StateId,
                        DesiredRequestsPerMinute = 0,
                        EffectiveRequestsPerMinute = 0,
                        RateAdjustment = 1,
                        IsUnderPressure = false,
                        RecentDownstreamFailurePercentage = 0,
                        Version = 0,
                        UpdatedAt = DateTime.UnixEpoch,
                    }
            );

            return Task.FromResult(_state);
        }
    }
}
