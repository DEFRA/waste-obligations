using System.ComponentModel.DataAnnotations;

namespace Defra.WasteObligations.Api.Dtos.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class ComplianceDeclarationSortListAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return ValidationResult.Success;

        return value is string stringValue && ComplianceDeclarationSortParser.TryParse(stringValue, out _)
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessage ?? $"Invalid {validationContext.DisplayName}");
    }
}
