using System.ComponentModel.DataAnnotations;
using System.Text.Json;
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

        if (value is not string stringValue)
            return new ValidationResult(ErrorMessage ?? $"Invalid {validationContext.DisplayName}");

        if (!IsDefined(stringValue))
            return new ValidationResult(
                $"{ErrorMessage ?? $"Invalid {validationContext.DisplayName}"} - {stringValue}"
            );

        return ValidationResult.Success;
    }

    private static bool IsDefined(string value)
    {
        try
        {
            var parsed = value.FromJsonValue<T>();

            return Enum.IsDefined(parsed) && parsed.ToJsonValue() == value;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
