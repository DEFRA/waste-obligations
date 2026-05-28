using System.ComponentModel.DataAnnotations;
using Defra.WasteObligations.Api.Extensions;

namespace Defra.WasteObligations.Api.Dtos.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class EnumCommaSeparatedListAttribute<T> : BaseCommaSeparatedListAttribute
    where T : struct, Enum
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var invalidValues = ParseInvalidValues(
            value,
            val =>
            {
                val.FromJsonValue<T>();

                return true;
            }
        );

        return HandleInvalidValues(invalidValues);
    }
}
