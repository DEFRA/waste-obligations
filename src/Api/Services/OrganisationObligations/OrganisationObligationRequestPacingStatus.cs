namespace Defra.WasteObligations.Api.Services.OrganisationObligations;

public record OrganisationObligationRequestPacingStatus
{
    public required int DesiredRequestsPerMinute { get; init; }

    public required int EffectiveRequestsPerMinute { get; init; }

    public string? BackoffReason { get; init; }

    public double? RecentDownstreamLatencyMilliseconds { get; init; }

    public required double RecentDownstreamFailurePercentage { get; init; }
}
