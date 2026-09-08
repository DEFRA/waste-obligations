namespace Defra.WasteObligations.Api.Dtos;

public record UnsubmittedPollingVolume
{
    public required int CurrentObligationYear { get; init; }
    public required int SourceOrganisationCount { get; init; }
    public required UnsubmittedPollingVolumeYear[] Years { get; init; }
}
