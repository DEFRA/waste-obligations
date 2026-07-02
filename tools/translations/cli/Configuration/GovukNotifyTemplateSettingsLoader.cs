using System.Text.Json;

namespace Translations.Configuration;

internal static class GovukNotifyTemplateSettingsLoader
{
    public static IReadOnlyList<GovukNotifyTemplateSettings> Load(string projectRoot, string appSettingsPath)
    {
        var resolvedPath = PathHelpers.ResolvePath(projectRoot, appSettingsPath);
        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException($"Appsettings file \"{resolvedPath}\" does not exist.", resolvedPath);
        }

        var json = File.ReadAllText(resolvedPath);
        var appSettings = JsonSerializer.Deserialize<AppSettingsFile>(
            json,
            new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            });

        var templates = appSettings?.GovukNotify.Templates;
        if (templates is null || templates.Count == 0)
        {
            throw new InvalidOperationException($"No GovukNotify:Templates entries were found in \"{resolvedPath}\".");
        }

        var settings = templates
            .OrderBy(template => template.Key, StringComparer.Ordinal)
            .Select(template => CreateTemplateSettings(template.Key, template.Value))
            .ToArray();

        if (settings.Length == 0)
        {
            throw new InvalidOperationException($"No GOV.UK Notify email templates with English template IDs were found in \"{resolvedPath}\".");
        }

        return settings;
    }

    private static GovukNotifyTemplateSettings CreateTemplateSettings(
        string templateName,
        GovukNotifyTemplate? template)
    {
        var templateId = template?.TemplateId;
        if (string.IsNullOrWhiteSpace(templateId?.En))
        {
            throw new InvalidOperationException($"GovukNotify:Templates:{templateName}:TemplateId:En is missing.");
        }

        return new GovukNotifyTemplateSettings(
            templateName,
            templateId.En,
            string.IsNullOrWhiteSpace(templateId.Cy) ? null : templateId.Cy);
    }

    private sealed class AppSettingsFile
    {
        public GovukNotifySettings GovukNotify { get; init; } = new();
    }

    private sealed class GovukNotifySettings
    {
        public Dictionary<string, GovukNotifyTemplate?> Templates { get; init; } = [];
    }

    private sealed class GovukNotifyTemplate
    {
        public GovukNotifyTemplateId TemplateId { get; init; } = new();
    }

    private sealed class GovukNotifyTemplateId
    {
        public string En { get; init; } = string.Empty;

        public string Cy { get; init; } = string.Empty;
    }
}
