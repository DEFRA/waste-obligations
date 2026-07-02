namespace Translations.Services;

internal static class DotEnvFile
{
    public static void LoadIfExists(string projectRoot)
    {
        var envPath = Path.Combine(projectRoot, ".env");
        if (!File.Exists(envPath))
        {
            return;
        }

        foreach (var rawLine in File.ReadLines(envPath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var name = line[..separatorIndex].Trim();
            var value = Unquote(line[(separatorIndex + 1)..].Trim());
            if (name.Length == 0 || Environment.GetEnvironmentVariable(name) is not null)
            {
                continue;
            }

            Environment.SetEnvironmentVariable(name, value);
        }
    }

    private static string Unquote(string value)
    {
        if (value.Length < 2)
        {
            return value;
        }

        return value[0] == value[^1] && (value[0] == '"' || value[0] == '\'')
            ? value[1..^1]
            : value;
    }
}
