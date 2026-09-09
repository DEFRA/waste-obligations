namespace Defra.WasteObligations.Api.Dtos;

public record UnsubmittedPollingVolumeYear
{
    public required int ObligationYear { get; init; }
    public required bool IsCurrentObligationYear { get; init; }
    public required int RegisteredOrganisationCount { get; init; }
    public required int RegisteredRegistrationCount { get; init; }
}
