using Translations.Services;

namespace Translations;

internal static class Cli
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (
            args.Length == 0
            || args.Contains("--help", StringComparer.OrdinalIgnoreCase)
            || args.Contains("-h", StringComparer.OrdinalIgnoreCase)
        )
        {
            WriteUsage();
            return 0;
        }

        try
        {
            var command = args[0].ToLowerInvariant();
            var options = CommandOptions.Parse(args.Skip(1).ToArray());
            var projectRoot = ProjectRootLocator.Find(options.ProjectRoot);

            DotEnvFile.LoadIfExists(projectRoot);

            return command switch
            {
                "export" => await ExportAsync(projectRoot, options),
                _ => UnknownCommand(command)
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await Console.Error.WriteLineAsync(exception.Message);
            return 1;
        }
    }

    private static async Task<int> ExportAsync(string projectRoot, CommandOptions options)
    {
        var apiKey = GovukNotifyApiKey.Get(options.ApiKeyEnvironmentVariable);
        var templateReader = new GovukNotifyTemplateReader(apiKey);
        return await new ExportService(templateReader).ExportAsync(projectRoot, options.AppSettings, options.Output);
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command \"{command}\".");
        WriteUsage();
        return 1;
    }

    private static void WriteUsage()
    {
        Console.WriteLine("GOV.UK Notify email translation workbook export tool");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project tools/translations/cli/cli.csproj -- export [--output translations/welsh-email-translations]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --output        Export output directory. Defaults to translations/welsh-email-translations.");
        Console.WriteLine("  --appsettings   API appsettings JSON path. Defaults to src/Api/appsettings.json.");
        Console.WriteLine("  --api-key-env   Environment variable name for the GOV.UK Notify API key.");
        Console.WriteLine("  --project-root  Repository root. Defaults to auto-detection from the current directory.");
    }
}
