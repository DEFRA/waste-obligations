namespace Defra.WasteObligations.Api.Services.WastePackaging;

public record ProducerPackaging
{
    public required Guid ProducerOrganisationId { get; init; }
    public IReadOnlyList<LeaverJoinerEvent> Events { get; init; } = [];
    public required IReadOnlyList<MaterialWeight> Materials { get; init; }
}
