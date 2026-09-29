using DotNetEnv;
using DotNetEnv.Configuration;
using Microsoft.Extensions.Configuration;

namespace Defra.WasteObligations.HealthCheck;

public sealed class HealthCheckConfiguration
{
    public int TimeoutSeconds { get; init; } = 5;
    public Dictionary<string, ServiceConfiguration> Services { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public static HealthCheckConfiguration Load(string path)
    {
        var fullPath = Path.GetFullPath(path);
        using var settings = new ConfigurationManager();
        settings
            .AddJsonFile(fullPath, optional: false, reloadOnChange: false)
            .AddDotNetEnv(Path.Combine(Path.GetDirectoryName(fullPath)!, ".env"), new LoadOptions(setEnvVars: false))
            .AddEnvironmentVariables();
        var configuration =
            settings.Get<HealthCheckConfiguration>() ?? throw new ArgumentException("Configure at least one service.");
        if (configuration.TimeoutSeconds is < 1 or > 5)
            throw new ArgumentException("TimeoutSeconds must be between 1 and 5.");
        if (configuration.Services is null || configuration.Services.Count == 0)
            throw new ArgumentException("Configure at least one service.");

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, service) in configuration.Services)
        {
            if (service is null || string.IsNullOrWhiteSpace(name) || name.Any(char.IsControl) || !names.Add(name))
                throw new ArgumentException(
                    "Service names must be non-empty, unique and contain no control characters."
                );
            ValidateAddress(service.Url);
            service.ApiKey?.Validate();
            service.OAuth?.Validate();
        }

        return configuration;
    }

    internal static void ValidateAddress(string address)
    {
        if (
            !Uri.TryCreate(address, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Fragment)
        )
            throw new ArgumentException(
                "URLs must use HTTPS (HTTP is allowed for loopback), without user information or fragments."
            );
    }

    internal static void ValidateCredential(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))
            throw new ArgumentException(
                "An authentication credential is missing, empty or contains control characters."
            );
    }
}
