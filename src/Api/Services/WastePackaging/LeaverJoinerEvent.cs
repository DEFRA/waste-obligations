namespace Defra.WasteObligations.Api.Services.WastePackaging;

public record LeaverJoinerEvent
{
    public required int Code { get; init; }
    public required EventAction Action { get; init; }
    public required DateOnly Date { get; init; }
}
