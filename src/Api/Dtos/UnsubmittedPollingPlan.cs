namespace Defra.WasteObligations.Api.Dtos;

public record UnsubmittedPollingPlan
{
    public required int CurrentObligationYear { get; init; }
    public required double TargetFullRefreshMinutes { get; init; }
    public required int SafetyCeilingRequestsPerMinute { get; init; }
    public required int RecommendedRateHeadroomPercentage { get; init; }
    public required bool SourceReadSucceeded { get; init; }
    public int? SourceOrganisationCount { get; init; }
    public required string[] Warnings { get; init; }
    public required UnsubmittedPollingPlanYear[] Years { get; init; }
}
