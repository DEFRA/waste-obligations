using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Defra.WasteObligations.Api.Dtos.Attributes;

namespace Defra.WasteObligations.Api.Dtos;

public record ComplianceDeclaration
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [Description("ISO 8601 extended format with offset")]
    [JsonPropertyName("created")]
    public DateTimeOffset Created { get; init; }

    [Description("ISO 8601 extended format with offset")]
    [JsonPropertyName("updated")]
    public DateTimeOffset Updated { get; init; }

    [JsonPropertyName("status")]
    public ComplianceDeclarationStatus Status { get; init; }

    [JsonPropertyName("organisation")]
    public required Organisation Organisation { get; init; }

    [Description("Obligation year of compliance declaration")]
    [JsonPropertyName("obligationYear")]
    public required int ObligationYear { get; init; }

    [JsonPropertyName("obligations")]
    public IEnumerable<Obligation> Obligations { get; init; } = [];

    [Required]
    [PossibleValue(Dtos.ObligationStatus.Met)]
    [PossibleValue(Dtos.ObligationStatus.NotMet)]
    [JsonPropertyName("obligationStatus")]
    public required string ObligationStatus { get; init; }

    [JsonPropertyName("submitterName")]
    public required string SubmitterName { get; init; }

    [JsonPropertyName("isRegulation43Compliant")]
    public bool IsRegulation43Compliant { get; init; }

    [JsonPropertyName("audit")]
    public IEnumerable<AuditEntry> Audit { get; init; } = [];
}
