using System.Collections.Frozen;

namespace Defra.WasteObligations.Api.Services.ObligationCalculations;

internal static class ObligationMaterials
{
    public const string Glass = "GL";
    public const string GlassRemelt = "GR";

    public static readonly FrozenSet<string> Inputs = new[] { "PL", "PC", "FC", "AL", "ST", "WD", Glass }.ToFrozenSet();

    public static readonly FrozenSet<string> Targets = Inputs.Append(GlassRemelt).ToFrozenSet();
}
