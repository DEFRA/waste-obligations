namespace Translations.Services;

internal static class GovukNotifyApiKey
{
    private static readonly string[] DefaultEnvironmentVariableNames =
    [
        "GovukNotify_ApiKey",
        "GovukNotify__ApiKey",
        "GOVUKNOTIFY_APIKEY",
        "GOVUK_NOTIFY_API_KEY"
    ];

    public static string Get(string? preferredEnvironmentVariableName)
    {
        var environmentVariableNames = string.IsNullOrWhiteSpace(preferredEnvironmentVariableName)
            ? DefaultEnvironmentVariableNames
            : [preferredEnvironmentVariableName, ..DefaultEnvironmentVariableNames];

        foreach (var name in environmentVariableNames.Distinct(StringComparer.Ordinal))
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        throw new InvalidOperationException(
            $"GOV.UK Notify API key was not found. Set one of: {string.Join(", ", environmentVariableNames.Distinct(StringComparer.Ordinal))}.");
    }
}
