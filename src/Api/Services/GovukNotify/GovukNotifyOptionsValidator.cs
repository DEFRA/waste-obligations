using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Services.GovukNotify;

public sealed class GovukNotifyOptionsValidator(IConfiguration configuration) : IValidateOptions<GovukNotifyOptions>
{
    public ValidateOptionsResult Validate(string? name, GovukNotifyOptions options)
    {
        var section = configuration
            .GetSection(GovukNotifyOptions.SectionName)
            .GetSection(nameof(GovukNotifyOptions.Templates));

        var invalidKeys = section
            .GetChildren()
            .Select(c => c.Key)
            .Where(key => !Enum.TryParse<GovukNotifyOptions.TemplateName>(key, ignoreCase: false, out _))
            .ToArray();

        return invalidKeys.Length != 0
            ? ValidateOptionsResult.Fail($"Invalid template names: {string.Join(", ", invalidKeys)}")
            : ValidateOptionsResult.Success;
    }
}
