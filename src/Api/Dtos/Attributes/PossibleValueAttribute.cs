#pragma warning disable CS9113 // Parameter is unread.
namespace Defra.WasteObligations.Api.Dtos.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class PossibleValueAttribute(string value) : Attribute;
