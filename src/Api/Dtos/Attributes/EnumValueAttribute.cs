using System.ComponentModel.DataAnnotations;
using Defra.WasteObligations.Api.Extensions;

namespace Defra.WasteObligations.Api.Dtos.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class EnumValueAttribute<T> : ValidationAttribute
    where T : struct, Enum
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return ValidationResult.Success;

        if (value is not string stringValue || !IsDefined(stringValue))
            return new ValidationResult(ErrorMessage ?? $"Invalid {validationContext.DisplayName}");

        return ValidationResult.Success;
    }

    private static bool IsDefined(string value)
    {
        if (!Enum.TryParse<T>(value, out var parsed) || !Enum.IsDefined(parsed))
            return false;

        return parsed.ToJsonValue() == value;
    }
}
