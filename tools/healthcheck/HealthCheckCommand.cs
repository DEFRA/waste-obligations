using System.Text.Json;

namespace Defra.WasteObligations.HealthCheck;

public static class HealthCheckCommand
{
    public static async Task<int> Run(
        string[] args,
        HttpClient client,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken
    )
    {
        if (args is ["--help"] or ["-h"])
        {
            await output.WriteLineAsync(
                "Usage: HealthCheck [appsettings.json] [--json]\nLoads .env beside the configuration.\nExit codes: 0 healthy, 1 failed/degraded, 2 configuration/usage error, 130 cancelled."
            );

            return 0;
        }
        var jsonOutput = args.Length > 0 && args[^1] == "--json";
        var paths = jsonOutput ? args[..^1] : args;
        if (paths.Length > 1 || (paths.Length == 1 && paths[0].StartsWith('-')))
        {
            await error.WriteLineAsync("Usage: HealthCheck [appsettings.json] [--json]");

            return 2;
        }

        HealthCheckConfiguration configuration;
        try
        {
            var path = Path.GetFullPath(paths.Length == 1 ? paths[0] : DefaultConfigurationPath());
            configuration = HealthCheckConfiguration.Load(path);
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or InvalidDataException
                        or UnauthorizedAccessException
                        or JsonException
                        or ArgumentException
                        or FormatException
                        or InvalidOperationException
                        or Superpower.ParseException
            )
        {
            await error.WriteLineAsync(
                "Invalid configuration. Check the file, service URLs, authentication headers and required environment variables; see tools/healthcheck/README.md."
            );

            return 2;
        }

        try
        {
            var runner = new HealthCheckRunner(client);
            var results = new List<HealthCheckResult>();
            foreach (var (name, service) in configuration.Services)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var progress = jsonOutput ? error : output;
                await progress.WriteLineAsync(
                    $"Checking {name}: GET {new Uri(service.Url).GetLeftPart(UriPartial.Path)} (timeout {configuration.TimeoutSeconds}s)"
                );
                await progress.FlushAsync(cancellationToken);
                var result = await runner.Check(name, service, configuration.TimeoutSeconds, cancellationToken);
                results.Add(result);
                if (!jsonOutput)
                {
                    await output.WriteLineAsync(
                        $"{(result.Healthy ? "PASS" : "FAIL")} {result.Name}: {result.Status} (HTTP {result.HttpStatus?.ToString() ?? "n/a"}, {result.DurationMs} ms)"
                    );
                    foreach (var check in result.Checks)
                        await output.WriteLineAsync($"  {JsonSerializer.Serialize(check.Key)}: {check.Value}");
                }
            }
            if (jsonOutput)
                await output.WriteLineAsync(
                    JsonSerializer.Serialize(
                        results,
                        new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }
                    )
                );

            return results.All(x => x.Healthy) ? 0 : 1;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("Health checks cancelled.");

            return 130;
        }
    }

    private static string DefaultConfigurationPath()
    {
        const string fileName = "appsettings.json";
        var repositoryPath = Path.Combine("tools", "healthcheck", fileName);
        if (File.Exists(repositoryPath))
            return repositoryPath;
        if (File.Exists(fileName))
            return fileName;

        return Path.Combine(AppContext.BaseDirectory, fileName);
    }
}
