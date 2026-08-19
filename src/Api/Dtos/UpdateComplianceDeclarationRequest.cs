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

    [JsonPropertyName("notification")]
    public NotificationRequest? Notification { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Status is ComplianceDeclarationStatus.Cancelled && string.IsNullOrWhiteSpace(Reason))
        {
            yield return new ValidationResult(
                "Reason is required when cancelling a compliance declaration.",
                [nameof(Reason)]
            );
        }

        if (Status is not ComplianceDeclarationStatus.Cancelled)
            yield break;

        if (Notification is null)
        {
            yield return new ValidationResult(
                "Notification is required when cancelling a compliance declaration.",
                [nameof(Notification)]
            );

            yield break;
        }

        if (Notification.Parameters is null || Notification.Parameters.Count == 0)
        {
            yield return new ValidationResult(
                "Notification parameters are required when cancelling a compliance declaration.",
                [$"{nameof(Notification)}.{nameof(NotificationRequest.Parameters)}"]
            );

            yield break;
        }

        foreach (var (key, value) in Notification.Parameters)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                yield return new ValidationResult(
                    "Notification parameter name must not be blank.",
                    [$"{nameof(Notification)}.{nameof(NotificationRequest.Parameters)}"]
                );
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                yield return new ValidationResult(
                    "Notification parameter value must not be blank.",
                    [$"{nameof(Notification)}.{nameof(NotificationRequest.Parameters)}"]
                );
            }
        }
    }
}
