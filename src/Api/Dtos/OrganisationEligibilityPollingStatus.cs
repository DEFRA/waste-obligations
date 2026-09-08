namespace Defra.WasteObligations.Api.Dtos;

public record OrganisationEligibilityPollingStatus
{
    public string? ActiveGeneration { get; init; }
    public required string? ActiveContentFingerprint { get; init; }
    public required int ActiveRowCount { get; init; }
    public DateTime? ActiveGenerationPromotedAt { get; init; }
    public DateTime? LastVerifiedAt { get; init; }
    public required long MaterialisedStateVersion { get; init; }
    public required bool RefreshPollingEnabled { get; init; }
    public required int RefreshPollIntervalSeconds { get; init; }
    public required int VisibleRowCount { get; init; }
    public required int HydrationEligibleOrganisationCount { get; init; }
    public required OrganisationReferenceResolutionStatus[] ReferenceResolutionStates { get; init; }
    public required PollingWorkerLeaseStatus Lease { get; init; }
}
