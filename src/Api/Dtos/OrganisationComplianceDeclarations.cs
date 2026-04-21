using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

public record OrganisationComplianceDeclarations
{
    [JsonPropertyName("complianceDeclarations")]
    public IEnumerable<ComplianceDeclaration> ComplianceDeclarations { get; init; } = [];
}
