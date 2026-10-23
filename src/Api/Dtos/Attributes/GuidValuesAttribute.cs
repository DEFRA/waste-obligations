using System.ComponentModel.DataAnnotations;

namespace Defra.WasteObligations.Api.Dtos.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public sealed class GuidValuesAttribute : ValidationAttribute
{
    public GuidValuesAttribute()
        : base("The {0} field must contain only GUIDs.") { }

    public override bool IsValid(object? value) =>
        value is null || value is IEnumerable<string> ids && ids.All(x => Guid.TryParseExact(x, "D", out _));
}
