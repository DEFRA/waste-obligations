using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Defra.WasteObligations.Api.Dtos.Attributes;

namespace Defra.WasteObligations.Api.Dtos;

public record UpdatePrnsRequest : IValidatableObject
{
    public const int MaxPrnIds = 100;

    [Required]
    [JsonPropertyName("status")]
    public UpdatePrnStatus? Status { get; init; }

    [Required]
    [JsonPropertyName("user")]
    public required User User { get; init; }

    [Required]
    [MinLength(1)]
    [MaxLength(MaxPrnIds)]
    [GuidValues]
    [JsonPropertyName("prnIds")]
    public List<string>? PrnIds { get; init; }

    [Range(Dtos.ObligationYear.Minimum, Dtos.ObligationYear.Maximum)]
    [JsonPropertyName("obligationYear")]
    public int? ObligationYear { get; init; }

    [JsonIgnore]
    internal IReadOnlyCollection<Guid> EffectivePrnIds =>
        [.. (PrnIds ?? throw new InvalidOperationException($"{nameof(PrnIds)} is only available after validation.")).Select(Guid.Parse).Distinct()];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // The PRN common backend records the year on the PRN whatever the status,
        // so a year is only meaningful, and only accepted, when accepting.
        if (ObligationYear is not null && Status != UpdatePrnStatus.Accepted)
        {
            yield return new ValidationResult(
                $"The {nameof(ObligationYear)} field can only be given when {nameof(Status)} is {nameof(UpdatePrnStatus.Accepted)}.",
                [nameof(ObligationYear)]
            );
        }
    }
}
