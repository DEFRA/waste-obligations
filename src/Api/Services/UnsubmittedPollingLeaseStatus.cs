using Defra.WasteObligations.Api.Dtos;

namespace Defra.WasteObligations.Api.Services;

public record UnsubmittedPollingLeaseStatus
{
    public required DateTime UtcNow { get; init; }
    public required PollingWorkerLeaseStatus Eligibility { get; init; }
    public required PollingWorkerLeaseStatus ObligationHydration { get; init; }
}
