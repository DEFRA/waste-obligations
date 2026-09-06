namespace Defra.WasteObligations.Api.Dtos;

public record PollingWorkerLeaseStatus
{
    public required bool IsHeld { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? LastReleasedAt { get; init; }
}
