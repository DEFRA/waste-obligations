using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Services.OrganisationObligations;

public static class OrganisationObligationRequestPacingController
{
    private const int SampleCount = 10;
    private const int MinimumSuccessfulReadSamplesForLatencyBackoff = 5;
    private const double FailureRateBackoffThresholdPercentage = 10;
    private const double LatencyIncreaseBackoffFactor = 1.5;
    private const double BackoffFactor = 0.8;
    private const double RecoveryIncrement = 0.1;

    public static OrganisationObligationRequestPacingState ObserveWorkload(
        OrganisationObligationRequestPacingState state,
        int activeSummaryCount,
        OrganisationObligationHydrationOptions options
    )
    {
        state = Normalise(state);
        state = state with { DesiredRequestsPerMinute = RequiredRequestsPerMinute(activeSummaryCount, options) };

        return UpdateEffectiveRate(state, options);
    }

    public static OrganisationObligationRequestPacingState ObserveRead(
        OrganisationObligationRequestPacingState state,
        TimeSpan duration,
        bool succeeded,
        OrganisationObligationHydrationOptions options
    )
    {
        state = Normalise(state);
        var recentReads = state
            .RecentReads.Append(
                new OrganisationObligationRequestPacingRead
                {
                    DurationMilliseconds = duration.TotalMilliseconds,
                    Succeeded = succeeded,
                }
            )
            .TakeLast(SampleCount)
            .ToArray();
        var averageLatencyMilliseconds = AverageSuccessfulLatencyMilliseconds(recentReads);
        var baselineLatencyMilliseconds = BaselineLatencyMilliseconds(state, recentReads, averageLatencyMilliseconds);

        state = state with
        {
            RecentReads = recentReads,
            BaselineDownstreamLatencyMilliseconds = baselineLatencyMilliseconds,
            RecentDownstreamLatencyMilliseconds = averageLatencyMilliseconds,
            RecentDownstreamFailurePercentage = FailurePercentage(recentReads),
        };

        if (state.RecentDownstreamFailurePercentage > FailureRateBackoffThresholdPercentage)
        {
            return BackOffIfNeeded(
                state,
                $"Downstream read failure rate is {state.RecentDownstreamFailurePercentage:0.#}%",
                !succeeded,
                options
            );
        }

        if (
            state.BaselineDownstreamLatencyMilliseconds is not null
            && state.RecentDownstreamLatencyMilliseconds is not null
            && state.RecentDownstreamLatencyMilliseconds
                > state.BaselineDownstreamLatencyMilliseconds * LatencyIncreaseBackoffFactor
        )
        {
            return BackOffIfNeeded(
                state,
                $"Downstream latency increased from {state.BaselineDownstreamLatencyMilliseconds:0.#}ms to {state.RecentDownstreamLatencyMilliseconds:0.#}ms",
                shouldCompoundBackoff: false,
                options
            );
        }

        state = state with
        {
            IsUnderPressure = false,
            RateAdjustment = Math.Min(1, state.RateAdjustment + RecoveryIncrement),
            BackoffReason = state.RateAdjustment + RecoveryIncrement >= 1 ? null : state.BackoffReason,
        };

        return UpdateEffectiveRate(state, options);
    }

    public static (OrganisationObligationRequestPacingState State, DateTime RequestAt) ReserveNextRequest(
        OrganisationObligationRequestPacingState state,
        DateTime utcNow
    )
    {
        state = Normalise(state);
        var requestAt = state.NextRequestAt is { } nextRequestAt && nextRequestAt > utcNow ? nextRequestAt : utcNow;
        var next =
            state.EffectiveRequestsPerMinute == 0
                ? requestAt
                : requestAt.Add(TimeSpan.FromMinutes(1d / state.EffectiveRequestsPerMinute));

        return (state with { NextRequestAt = next }, requestAt);
    }

    public static OrganisationObligationRequestPacingStatus Status(OrganisationObligationRequestPacingState? state)
    {
        if (state is null)
        {
            return new OrganisationObligationRequestPacingStatus
            {
                DesiredRequestsPerMinute = 0,
                EffectiveRequestsPerMinute = 0,
                RecentDownstreamFailurePercentage = 0,
            };
        }

        state = Normalise(state);

        return new OrganisationObligationRequestPacingStatus
        {
            DesiredRequestsPerMinute = state.DesiredRequestsPerMinute,
            EffectiveRequestsPerMinute = state.EffectiveRequestsPerMinute,
            BackoffReason = state.BackoffReason,
            RecentDownstreamLatencyMilliseconds = state.RecentDownstreamLatencyMilliseconds,
            RecentDownstreamFailurePercentage = state.RecentDownstreamFailurePercentage,
        };
    }

    private static OrganisationObligationRequestPacingState BackOffIfNeeded(
        OrganisationObligationRequestPacingState state,
        string reason,
        bool shouldCompoundBackoff,
        OrganisationObligationHydrationOptions options
    )
    {
        state = state with
        {
            IsUnderPressure = true,
            RateAdjustment =
                !state.IsUnderPressure || shouldCompoundBackoff
                    ? state.RateAdjustment * BackoffFactor
                    : state.RateAdjustment,
            BackoffReason = reason,
        };

        return UpdateEffectiveRate(state, options);
    }

    private static double? BaselineLatencyMilliseconds(
        OrganisationObligationRequestPacingState state,
        IEnumerable<OrganisationObligationRequestPacingRead> recentReads,
        double? averageLatencyMilliseconds
    )
    {
        if (
            averageLatencyMilliseconds is null
            || SuccessfulReadCount(recentReads) < MinimumSuccessfulReadSamplesForLatencyBackoff
        )
        {
            return state.BaselineDownstreamLatencyMilliseconds;
        }

        return state.BaselineDownstreamLatencyMilliseconds is { } baselineLatencyMilliseconds
            ? Math.Min(baselineLatencyMilliseconds, averageLatencyMilliseconds.Value)
            : averageLatencyMilliseconds;
    }

    private static double? AverageSuccessfulLatencyMilliseconds(
        IEnumerable<OrganisationObligationRequestPacingRead> recentReads
    )
    {
        var successfulReads = recentReads.Where(x => x.Succeeded).Select(x => x.DurationMilliseconds).ToArray();

        return successfulReads.Length == 0 ? null : successfulReads.Average();
    }

    private static double FailurePercentage(IEnumerable<OrganisationObligationRequestPacingRead> recentReads)
    {
        var reads = recentReads.ToArray();

        return reads.Length == 0 ? 0 : 100d * reads.Count(x => !x.Succeeded) / reads.Length;
    }

    private static OrganisationObligationRequestPacingState Normalise(OrganisationObligationRequestPacingState state) =>
        state with
        {
            RateAdjustment = state.RateAdjustment > 0 ? state.RateAdjustment : 1,
            RecentReads = state.RecentReads ?? [],
        };

    private static int RequiredRequestsPerMinute(
        int activeSummaryCount,
        OrganisationObligationHydrationOptions options
    ) => activeSummaryCount == 0 ? 0 : (int)Math.Ceiling(activeSummaryCount / options.RefreshInterval.TotalMinutes);

    private static int SuccessfulReadCount(IEnumerable<OrganisationObligationRequestPacingRead> recentReads) =>
        recentReads.Count(x => x.Succeeded);

    private static OrganisationObligationRequestPacingState UpdateEffectiveRate(
        OrganisationObligationRequestPacingState state,
        OrganisationObligationHydrationOptions options
    )
    {
        var unconstrainedRate = Math.Min(
            state.DesiredRequestsPerMinute,
            Math.Min(options.MaxDownstreamRequestsPerMinute, MaximumSustainableRequestsPerMinute(state, options))
        );

        return state with
        {
            EffectiveRequestsPerMinute =
                unconstrainedRate == 0 ? 0 : Math.Max(1, (int)Math.Ceiling(unconstrainedRate * state.RateAdjustment)),
        };
    }

    private static int MaximumSustainableRequestsPerMinute(
        OrganisationObligationRequestPacingState state,
        OrganisationObligationHydrationOptions options
    )
    {
        var averageLatencyMilliseconds = state.RecentDownstreamLatencyMilliseconds;
        if (averageLatencyMilliseconds is null || averageLatencyMilliseconds == 0)
        {
            return int.MaxValue;
        }

        var maximumRequestsPerMinute =
            options.MaxConcurrentRequests
            * TimeSpan.FromMinutes(1).TotalMilliseconds
            / averageLatencyMilliseconds.Value;

        return maximumRequestsPerMinute >= int.MaxValue
            ? int.MaxValue
            : Math.Max(1, (int)Math.Floor(maximumRequestsPerMinute));
    }
}
