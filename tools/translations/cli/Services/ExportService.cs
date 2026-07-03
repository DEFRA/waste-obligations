using System.Text.Encodings.Web;
using System.Text.Json;
using Translations.Configuration;
using Translations.Models;

namespace Translations.Services;

internal sealed class ExportService(IGovukNotifyTemplateReader templateReader)
{
    public const string DefaultOutputPath = "translations/welsh-email-translations";

    private static readonly string[] TranslatorInstructions =
    [
        "Preserve GOV.UK Notify personalisation placeholders such as ((regulator)) and ((obligationYear)).",
        "Preserve Markdown formatting, links, headings and blank lines.",
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public async Task<int> ExportAsync(string projectRoot, string appSettingsPath, string? outputPath)
    {
        var templateSettings = GovukNotifyTemplateSettingsLoader.Load(projectRoot, appSettingsPath);
        var resolvedOutputPath = PathHelpers.ResolvePath(projectRoot, outputPath ?? DefaultOutputPath);
        var textExportOutputPath = Path.Combine(resolvedOutputPath, "json");
        Directory.CreateDirectory(resolvedOutputPath);
        Directory.CreateDirectory(textExportOutputPath);

        var groups = new List<EmailTranslationGroup>();
        for (var index = 0; index < templateSettings.Count; index++)
        {
            groups.Add(await BuildGroupAsync(templateSettings[index], index + 1));
        }

        var workbookCounts = new ExportStatusCounts();
        var textExportCounts = new ExportStatusCounts();
        var totalRows = 0;
        foreach (var group in groups)
        {
            var workbookPath = Path.Combine(resolvedOutputPath, group.FileName);
            var textExportPath = Path.Combine(textExportOutputPath, GetTextExportFileName(group.FileName));
            var exportData = BuildExportData(group, TranslatorInstructions);
            var workbookStatus = await WriteWorkbookIfChangedAsync(
                workbookPath,
                group,
                TranslatorInstructions,
                exportData
            );
            var textExportStatus = await WriteTextExportIfChangedAsync(textExportPath, exportData);

            workbookCounts.Add(workbookStatus);
            textExportCounts.Add(textExportStatus);
            totalRows += group.Rows.Count;

            Console.WriteLine(
                $"Processed {workbookPath} ({group.Rows.Count} row{Plural(group.Rows.Count)}; workbook: {workbookStatus}; JSON: {textExportStatus})"
            );
        }

        Console.WriteLine($"Workbooks: {workbookCounts}");
        Console.WriteLine($"JSON sidecars: {textExportCounts}");
        Console.WriteLine($"Included {totalRows} translation row{Plural(totalRows)}");

        return 0;
    }

    private static async Task<string> WriteWorkbookIfChangedAsync(
        string workbookPath,
        EmailTranslationGroup group,
        IReadOnlyList<string> translatorInstructions,
        EmailTranslationExportData expectedExportData
    )
    {
        var outputExists = File.Exists(workbookPath);
        if (outputExists && await WorkbookMatchesExportDataAsync(workbookPath, expectedExportData))
        {
            return ExportStatus.Unchanged;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(workbookPath)!);
        await XlsxWorkbookWriter.WriteAsync(workbookPath, group, translatorInstructions);

        return outputExists ? ExportStatus.Updated : ExportStatus.Created;
    }

    private static async Task<bool> WorkbookMatchesExportDataAsync(
        string workbookPath,
        EmailTranslationExportData expectedExportData
    )
    {
        try
        {
            var actualExportData = await XlsxWorkbookReader.ReadExportDataAsync(workbookPath);

            return ExportDataEquals(actualExportData, expectedExportData);
        }
        catch
        {
            return false;
        }
    }

    private static bool ExportDataEquals(EmailTranslationExportData actual, EmailTranslationExportData expected)
    {
        return actual.TranslatorNotes.SequenceEqual(expected.TranslatorNotes, StringComparer.Ordinal)
            && actual.Rows.SequenceEqual(expected.Rows);
    }

    private static async Task<string> WriteTextExportIfChangedAsync(
        string textExportPath,
        EmailTranslationExportData exportData
    )
    {
        var outputExists = File.Exists(textExportPath);
        var content = $"{JsonSerializer.Serialize(exportData, JsonOptions)}\n";

        if (
            outputExists
            && string.Equals(await File.ReadAllTextAsync(textExportPath), content, StringComparison.Ordinal)
        )
        {
            return ExportStatus.Unchanged;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(textExportPath)!);
        await File.WriteAllTextAsync(textExportPath, content);

        return outputExists ? ExportStatus.Updated : ExportStatus.Created;
    }

    private static EmailTranslationExportData BuildExportData(
        EmailTranslationGroup group,
        IReadOnlyList<string> translatorInstructions
    )
    {
        return new EmailTranslationExportData(translatorInstructions, group.Rows);
    }

    private static string GetTextExportFileName(string workbookFileName)
    {
        var textExportFileName = $"{Path.GetFileNameWithoutExtension(workbookFileName)}.json";

        return textExportFileName;
    }

    private async Task<EmailTranslationGroup> BuildGroupAsync(GovukNotifyTemplateSettings settings, int sequenceNumber)
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
            rows
        );
    }

    private static IEnumerable<EmailTranslationRow> BuildRows(
        GovukNotifyTemplateSettings settings,
        EmailTemplateContent englishTemplate,
        EmailTemplateContent? welshTemplate
    )
    {
        if (!string.IsNullOrEmpty(englishTemplate.Subject))
        {
            yield return CreateRow(settings, "Subject", englishTemplate.Subject, welshTemplate?.Subject);
        }

        if (!string.IsNullOrEmpty(englishTemplate.Body))
        {
            yield return CreateRow(settings, "Body", englishTemplate.Body, welshTemplate?.Body);
        }
    }

    private static EmailTranslationRow CreateRow(
        GovukNotifyTemplateSettings settings,
        string field,
        string english,
        string? existingWelsh
    )
    {
        var welsh =
            !string.IsNullOrWhiteSpace(existingWelsh)
            && !string.Equals(existingWelsh, english, StringComparison.Ordinal)
                ? existingWelsh
                : string.Empty;

        return new EmailTranslationRow(
            $"GovukNotify:Templates:{settings.Name}:{field}",
            settings.Name,
            settings.EnglishTemplateId,
            field,
            english,
            welsh
        );
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
        char.IsLower(value[index - 1]) || index + 1 < value.Length && char.IsLower(value[index + 1]);

    private static string Plural(int count) => count == 1 ? string.Empty : "s";

    private static class ExportStatus
    {
        public const string Created = "created";
        public const string Updated = "updated";
        public const string Unchanged = "unchanged";
    }

    private sealed class ExportStatusCounts
    {
        private int Created { get; set; }

        private int Updated { get; set; }

        private int Unchanged { get; set; }

        public void Add(string status)
        {
            switch (status)
            {
                case ExportStatus.Created:
                    Created++;
                    break;
                case ExportStatus.Updated:
                    Updated++;
                    break;
                case ExportStatus.Unchanged:
                    Unchanged++;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown export status \"{status}\".");
            }
        }

        public override string ToString()
        {
            var counts = $"created {Created}, updated {Updated}, unchanged {Unchanged}";

            return counts;
        }
    }
}
