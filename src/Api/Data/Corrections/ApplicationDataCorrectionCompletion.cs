namespace Defra.WasteObligations.Api.Data.Corrections;

public record ApplicationDataCorrectionCompletion
{
    public required string Id { get; init; }
    public required int Version { get; init; }
    public required DateTime CompletedAt { get; init; }
}
