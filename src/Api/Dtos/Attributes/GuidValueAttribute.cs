using System.ComponentModel.DataAnnotations;

namespace Defra.WasteObligations.Api.Dtos.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public sealed class GuidValueAttribute : ValidationAttribute
{
    public GuidValueAttribute()
        : base("The {0} field must be a GUID.") { }

    public override bool IsValid(object? value) => value is null || value is string id && Guid.TryParse(id, out _);
}
