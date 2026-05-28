using System.ComponentModel.DataAnnotations;

namespace Defra.WasteObligations.Api.Dtos.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class MinimumAttribute : RangeAttribute
{
    public MinimumAttribute(int minimum)
        : base(minimum, int.MaxValue)
    {
        ErrorMessage = "The field {0} must be {1} or more.";
    }
}
