using Translations.Configuration;
using Translations.Models;

namespace Translations.Services;

internal sealed class ExportService(IGovukNotifyTemplateReader templateReader)
{
    public const string DefaultOutputPath = "translations/welsh-email-translations";

    private static readonly string[] TranslatorInstructions =
    [
        "Preserve GOV.UK Notify personalisation placeholders such as ((regulator)) and ((obligationYear)).",
        "Preserve Markdown formatting, links, headings and blank lines."
    ];

    public async Task<int> ExportAsync(string projectRoot, string appSettingsPath, string? outputPath)
    {
        var templateSettings = GovukNotifyTemplateSettingsLoader.Load(projectRoot, appSettingsPath);
        var resolvedOutputPath = PathHelpers.ResolvePath(projectRoot, outputPath ?? DefaultOutputPath);
        Directory.CreateDirectory(resolvedOutputPath);

        var groups = new List<EmailTranslationGroup>();
        for (var index = 0; index < templateSettings.Count; index++)
        {
            groups.Add(await BuildGroupAsync(templateSettings[index], index + 1));
        }

        var totalRows = 0;
        foreach (var group in groups)
        {
            var workbookPath = Path.Combine(resolvedOutputPath, group.FileName);
            await XlsxWorkbookWriter.WriteAsync(workbookPath, group, TranslatorInstructions);
            totalRows += group.Rows.Count;

            Console.WriteLine($"Created {workbookPath} ({group.Rows.Count} row{Plural(group.Rows.Count)})");
        }

        Console.WriteLine($"Created {groups.Count} translation workbook{Plural(groups.Count)}");
        Console.WriteLine($"Included {totalRows} translation row{Plural(totalRows)}");
        return 0;
    }

    private async Task<EmailTranslationGroup> BuildGroupAsync(
        GovukNotifyTemplateSettings settings,
        int sequenceNumber)
    {
        var englishTemplate = await templateReader.GetTemplateAsync(settings.EnglishTemplateId);
        var welshTemplate = settings.WelshTemplateId is null
            ? null
            : await templateReader.GetTemplateAsync(settings.WelshTemplateId);

        var rows = BuildRows(settings, englishTemplate, welshTemplate).ToArray();

        return new EmailTranslationGroup(
            settings.Name,
            $"{sequenceNumber:00}-{ToKebabCase(settings.Name)}.xlsx",
            settings.Name,
            settings.EnglishTemplateId,
            settings.WelshTemplateId,
            rows);
    }

    private static IEnumerable<EmailTranslationRow> BuildRows(
        GovukNotifyTemplateSettings settings,
        EmailTemplateContent englishTemplate,
        EmailTemplateContent? welshTemplate)
    {
        if (!string.IsNullOrEmpty(englishTemplate.Subject))
        {
            yield return CreateRow(
                settings,
                "Subject",
                englishTemplate.Subject,
                welshTemplate?.Subject);
        }

        if (!string.IsNullOrEmpty(englishTemplate.Body))
        {
            yield return CreateRow(
                settings,
                "Body",
                englishTemplate.Body,
                welshTemplate?.Body);
        }
    }

    private static EmailTranslationRow CreateRow(
        GovukNotifyTemplateSettings settings,
        string field,
        string english,
        string? existingWelsh)
    {
        var welsh = !string.IsNullOrWhiteSpace(existingWelsh) && !string.Equals(existingWelsh, english, StringComparison.Ordinal)
            ? existingWelsh
            : string.Empty;

        return new EmailTranslationRow(
            $"GovukNotify:Templates:{settings.Name}:{field}",
            settings.Name,
            settings.EnglishTemplateId,
            field,
            english,
            welsh);
    }

    private static string ToKebabCase(string value)
    {
        var characters = new List<char>(value.Length * 2);

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsUpper(character) && index > 0 && IsKebabBoundary(value, index))
            {
                characters.Add('-');
            }

            characters.Add(char.ToLowerInvariant(character));
        }

        return new string(characters.ToArray());
    }

    private static bool IsKebabBoundary(string value, int index) =>
        char.IsLower(value[index - 1])
        || index + 1 < value.Length && char.IsLower(value[index + 1]);

    private static string Plural(int count) => count == 1 ? string.Empty : "s";
}
