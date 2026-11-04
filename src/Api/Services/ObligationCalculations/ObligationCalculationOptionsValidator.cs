using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Services.ObligationCalculations;

public class ObligationCalculationOptionsValidator(IConfiguration configuration)
    : IValidateOptions<ObligationCalculationOptions>
{
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
                || x.Value.Count != ObligationMaterials.Targets.Count
                || !ObligationMaterials.Targets.SetEquals(x.Value.Keys)
                || x.Value.Values.Any(value => !double.IsFinite(value) || value < 0 || value > 1)
            )
        )
        {
            return ValidateOptionsResult.Fail(
                $"Each obligation year must have one valid target between zero and one for {string.Join(", ", ObligationMaterials.Targets.Order())}"
            );
        }

        return ValidateOptionsResult.Success;
    }
}
