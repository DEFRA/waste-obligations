namespace Defra.WasteObligations.Api.Dtos;

public record UnsubmittedPollingPlanYear
{
    public required int ObligationYear { get; init; }
    public required string Classification { get; init; }
    public required int PotentialHydrationOrganisationCount { get; init; }
    public required int RegisteredRegistrationCount { get; init; }
    public required int RequiredRequestsPerMinute { get; init; }
    public required int RecommendedRequestsPerMinute { get; init; }
    public required double EstimatedFullRefreshMinutesAtSafetyCeiling { get; init; }
}
