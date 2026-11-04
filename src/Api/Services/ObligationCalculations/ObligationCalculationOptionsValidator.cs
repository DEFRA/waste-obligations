using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Services.ObligationCalculations;

public class ObligationCalculationOptionsValidator(IConfiguration configuration)
    : IValidateOptions<ObligationCalculationOptions>
{
    private static readonly HashSet<string> TargetMaterials = ["PL", "PC", "FC", "AL", "ST", "WD", "GL", "GR"];

    public ValidateOptionsResult Validate(string? name, ObligationCalculationOptions options)
    {
        var years = configuration.GetSection($"{ObligationCalculationOptions.SectionName}:Targets").GetChildren();
        if (
            years.Any(x =>
                !int.TryParse(x.Key, out var year)
                || year <= 0
                || x.Key != year.ToString(System.Globalization.CultureInfo.InvariantCulture)
            )
            || options.Targets.Count == 0
            || options.Targets.Any(x =>
                x.Key <= 0
                || x.Value is null
                || x.Value.Count != TargetMaterials.Count
                || !TargetMaterials.SetEquals(x.Value.Keys)
                || x.Value.Values.Any(value => !double.IsFinite(value) || value < 0 || value > 1)
            )
        )
        {
            return ValidateOptionsResult.Fail(
                "Each obligation year must have one valid target between zero and one for PL, PC, FC, AL, ST, WD, GL and GR"
            );
        }

        return ValidateOptionsResult.Success;
    }
}
