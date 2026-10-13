namespace Defra.WasteObligations.Api.Dtos;

public record UnsubmittedPollingConfiguration
{
    public bool EligibilityRefreshPollingEnabled { get; init; }
    public int OrganisationPollIntervalSeconds { get; init; }
    public bool ObligationPollingEnabled { get; init; }
    public double ObligationRefreshIntervalSeconds { get; init; }
    public double MaximumSummaryStalenessSeconds { get; init; }
    public int MaxDownstreamRequestsPerMinute { get; init; }
    public int ObligationPollIntervalSeconds { get; init; }
    public int CurrentObligationYear { get; init; }
}
