namespace Translations;

internal sealed class CommandOptions
{
    public string? Output { get; private init; }

    public string AppSettings { get; private init; } = "src/Api/appsettings.json";

    public string? ApiKeyEnvironmentVariable { get; private init; }

    public string? ProjectRoot { get; private init; }

    public static CommandOptions Parse(IReadOnlyList<string> args)
    {
        string? output = null;
        var appSettings = "src/Api/appsettings.json";
        string? apiKeyEnvironmentVariable = null;
        string? projectRoot = null;

        for (var index = 0; index < args.Count; index++)
        {
            var option = args[index];
            switch (option)
            {
                case "--output":
                    output = ReadValue(args, ref index, option);
                    break;
                case "--appsettings":
                    appSettings = ReadValue(args, ref index, option);
                    break;
                case "--api-key-env":
                    apiKeyEnvironmentVariable = ReadValue(args, ref index, option);
                    break;
                case "--project-root":
                    projectRoot = ReadValue(args, ref index, option);
                    break;
                default:
                    throw new ArgumentException($"Unknown option \"{option}\".");
            }
        }

        return new CommandOptions
        {
            Output = output,
            AppSettings = appSettings,
            ApiKeyEnvironmentVariable = apiKeyEnvironmentVariable,
            ProjectRoot = projectRoot
        };
    }

    private static string ReadValue(IReadOnlyList<string> args, ref int index, string option)
    {
        if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Missing value for {option}.");
        }

        index++;
        return args[index];
    }
}
