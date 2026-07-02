namespace Translations;

internal static class PathHelpers
{
    public static string ResolvePath(string projectRoot, string path) =>
        Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(projectRoot, path));
}
