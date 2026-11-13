using System.Collections.Frozen;

namespace Defra.WasteObligations.Api.Services.ObligationCalculations;

internal static class ObligationMaterials
{
    public const string Plastic = "PL";
    public const string PaperCard = "PC";
    public const string FibreComposite = "FC";
    public const string Aluminium = "AL";
    public const string Steel = "ST";
    public const string Wood = "WD";
    public const string Glass = "GL";
    public const string GlassRemelt = "GR";

    public static readonly FrozenSet<string> Inputs = new[]
    {
        Plastic,
        PaperCard,
        FibreComposite,
        Aluminium,
        Steel,
        Wood,
        Glass,
    }.ToFrozenSet();

    public static readonly FrozenSet<string> Targets = Inputs.Append(GlassRemelt).ToFrozenSet();
}
