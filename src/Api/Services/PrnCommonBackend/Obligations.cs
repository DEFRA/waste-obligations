using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Services.PrnCommonBackend;

public record Obligations
{
    [JsonPropertyName("numberOfPrnsAwaitingAcceptance")]
    public int NumberOfPrnsAwaitingAcceptance { get; init; }

    [JsonPropertyName("obligationData")]
    public Obligation[] ObligationData { get; init; } = [];
}
