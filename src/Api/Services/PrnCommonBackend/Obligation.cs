using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Services.PrnCommonBackend;

public record Obligation
{
    [JsonPropertyName("organisationId")]
    public Guid OrganisationId { get; init; }

    [JsonPropertyName("materialName")]
    public required string MaterialName { get; init; }

    [JsonPropertyName("tonnage")]
    public int Tonnage { get; init; }

    [JsonPropertyName("materialTarget")]
    public decimal MaterialTarget { get; init; }

    [JsonPropertyName("obligationToMeet")]
    public int? ObligationToMeet { get; init; }

    [JsonPropertyName("tonnageAwaitingAcceptance")]
    public int TonnageAwaitingAcceptance { get; init; }

    [JsonPropertyName("tonnageAccepted")]
    public int TonnageAccepted { get; init; }

    [JsonPropertyName("tonnageOutstanding")]
    public int? TonnageOutstanding { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }
}
