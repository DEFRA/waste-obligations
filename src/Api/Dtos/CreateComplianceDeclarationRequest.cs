using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Defra.WasteObligations.Api.Dtos.Attributes;

namespace Defra.WasteObligations.Api.Dtos;

public record CreateComplianceDeclarationRequest
{
    [Required]
    [JsonPropertyName("organisation")]
    public required OrganisationRequest Organisation { get; init; }

    [Required]
    [Range(Dtos.ObligationYear.Minimum, Dtos.ObligationYear.Maximum)]
    [JsonPropertyName("obligationYear")]
    public required int ObligationYear { get; init; }

    [JsonPropertyName("obligations")]
    public IEnumerable<Obligation> Obligations { get; init; } = [];

    [Required]
    [PossibleValue(Dtos.ObligationStatus.Met)]
    [PossibleValue(Dtos.ObligationStatus.NotMet)]
    [JsonPropertyName("obligationStatus")]
    public required string ObligationStatus { get; init; }

    [Required]
    [JsonPropertyName("declarationText")]
    public required LocalizedText DeclarationText { get; init; }

    [Required]
    [JsonPropertyName("submitterName")]
    public required string SubmitterName { get; init; }

    [Required]
    [JsonPropertyName("user")]
    public required User User { get; init; }
}
