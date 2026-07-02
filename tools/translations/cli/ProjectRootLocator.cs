namespace Translations;

internal static class ProjectRootLocator
{
    public static string Find(string? explicitProjectRoot)
    {
        if (!string.IsNullOrWhiteSpace(explicitProjectRoot))
        {
            var resolvedPath = Path.GetFullPath(explicitProjectRoot);
            if (!Directory.Exists(resolvedPath))
            {
                throw new DirectoryNotFoundException($"Project root \"{resolvedPath}\" does not exist.");
            }

            return resolvedPath;
        }

        var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (currentDirectory is not null)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, "waste-obligations.slnx")))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root. Pass --project-root to set it explicitly.");
    }
}
