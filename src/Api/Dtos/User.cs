using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Defra.WasteObligations.Api.Dtos.Attributes;

namespace Defra.WasteObligations.Api.Dtos;

public record User : IValidatableObject
{
    [Required]
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [Required]
    [JsonPropertyName("email")]
    public required string Email { get; init; }

    [Required]
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [Required]
    [PossibleValue(UserLocale.En)]
    [PossibleValue(UserLocale.Cy)]
    [JsonPropertyName("locale")]
    public string? Locale { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Locale is not null && Locale is not (UserLocale.En or UserLocale.Cy))
        {
            yield return new ValidationResult(
                $"The field {nameof(Locale)} must be one of: {UserLocale.En}, {UserLocale.Cy}.",
                [nameof(Locale)]
            );
        }
    }
}
