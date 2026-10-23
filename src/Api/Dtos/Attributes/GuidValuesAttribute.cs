using System.ComponentModel.DataAnnotations;

namespace Defra.WasteObligations.Api.Dtos.Attributes;

// PRN IDs must be hyphenated ("D" format) because the frontend schema allows only that form.
// This is deliberately stricter than GuidValueAttribute (used by User.Id), which accepts any
// format Guid.TryParse accepts. Keep the two rules separate unless the frontend contract changes.
[AttributeUsage(AttributeTargets.Property)]
public sealed class GuidValuesAttribute : ValidationAttribute
{
    public GuidValuesAttribute()
        : base("The {0} field must contain only GUIDs.") { }

    public override bool IsValid(object? value) =>
        value is null || value is IEnumerable<string> ids && ids.All(x => Guid.TryParseExact(x, "D", out _));
}
