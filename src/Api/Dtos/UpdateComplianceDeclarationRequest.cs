using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

public record UpdateComplianceDeclarationRequest : IValidatableObject
{
    [JsonPropertyName("status")]
    public ComplianceDeclarationStatus? Status { get; init; }

    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    [Required]
    [JsonPropertyName("user")]
    public required User User { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Status.HasValue && string.IsNullOrWhiteSpace(Reason))
        {
            yield return new ValidationResult("Reason is required when status is provided.", [nameof(Reason)]);
        }
    }
}
