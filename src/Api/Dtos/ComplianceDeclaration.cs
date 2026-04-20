using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

public record ComplianceDeclaration
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("organisationId")]
    public Guid OrganisationId { get; init; }

    [Description("Obligation year of compliance declaration")]
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
