namespace Defra.WasteObligations.Api.Utils.Metrics;

public interface IOrganisationObligationHydrationMetrics
{
    void Failed();

    void LeaseNotAcquired();

    void ObligationReadCompleted(TimeSpan duration);

    void ObligationReadFailed(TimeSpan duration);

    void QueueObserved(int activeSummaryCount, int dueSummaryCount);

    void CapacityObserved(
        int activeSummaryCount,
        int maxDownstreamRequestsPerMinute,
        int desiredRequestsPerMinute,
        int effectiveRequestsPerMinute,
        TimeSpan refreshInterval
    );

    void Succeeded();

    void StalenessObserved(int staleSummaryCount, double oldestStaleSummaryAgeSeconds);
}
