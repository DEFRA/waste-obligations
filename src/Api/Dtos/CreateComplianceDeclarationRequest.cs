using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

public record CreateComplianceDeclarationRequest
{
    [Description("Obligation year of compliance declaration")]
    [Range(Dtos.ObligationYear.Minimum, Dtos.ObligationYear.Maximum)]
    [JsonPropertyName("obligationYear")]
    public required int ObligationYear { get; init; }

    [JsonPropertyName("obligations")]
    public IEnumerable<Obligation> Obligations { get; init; } = [];

    [JsonPropertyName("declarationText")]
    public required LocalizedText DeclarationText { get; init; }

    [JsonPropertyName("submitterName")]
    public required string SubmitterName { get; init; }

    [JsonPropertyName("user")]
    public required User User { get; init; }
}
