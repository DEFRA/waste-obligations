namespace Defra.WasteObligations.Api.Dtos;

public static class Material
{
    public const string Plastic = nameof(Plastic);
    public const string Glass = nameof(Glass);
    public const string Aluminium = nameof(Aluminium);
    public const string Steel = nameof(Steel);
    public const string Wood = nameof(Wood);
    public const string GlassRemelt = nameof(GlassRemelt);
    public const string FibreComposite = nameof(FibreComposite);

    public static readonly string[] All = [Plastic, Glass, Aluminium, Steel, Wood, GlassRemelt, FibreComposite];
}
