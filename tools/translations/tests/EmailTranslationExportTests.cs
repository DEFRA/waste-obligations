using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using Translations.Models;
using Translations.Services;
using Xunit;

namespace Translations.Tests;

public sealed class EmailTranslationExportTests : IDisposable
{
    private readonly string _projectRoot;

    public EmailTranslationExportTests()
    {
        _projectRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_projectRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_projectRoot))
        {
            Directory.Delete(_projectRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExportAsync_PullsConfiguredEnglishTemplateAndWritesWorkbook()
    {
        WriteAppSettings(
            """
            {
              "GovukNotify": {
                "ApiKey": "unused",
                "Templates": {
                  "ComplianceDeclarationSubmissionDirectProducer": {
                    "TemplateId": {
                      "En": "en-template-id",
                      "Cy": "cy-template-id"
                    }
                  }
                }
              }
            }
            """
        );

        const string englishBody =
            "Hello ((regulator))\n\nThis is a long GOV.UK Notify email body that should wrap across more than one worksheet line so the generated row height grows.";
        var reader = new FakeTemplateReader(
            new EmailTemplateContent("en-template-id", "Certificate received", englishBody),
            new EmailTemplateContent("cy-template-id", "Tystysgrif wedi dod i law", "Welsh body")
        );

        var result = await new ExportService(reader).ExportAsync(_projectRoot, "src/Api/appsettings.json", "exports");

        Assert.Equal(0, result);
        Assert.Equal(["en-template-id", "cy-template-id"], reader.RequestedTemplateIds);

        var workbookPath = Path.Combine(
            _projectRoot,
            "exports",
            "01-compliance-declaration-submission-direct-producer.xlsx"
        );
        var textExportPath = Path.Combine(
            _projectRoot,
            "exports",
            "json",
            "01-compliance-declaration-submission-direct-producer.json"
        );
        Assert.True(File.Exists(workbookPath));
        Assert.True(File.Exists(textExportPath));
        AssertWorkbookRelationshipsUsePackageNamespace(workbookPath);
        AssertTranslatorNotes(workbookPath);

        var rows = ReadWorkbookRows(workbookPath);
        Assert.Equal(2, rows.Count);

        Assert.Equal("GovukNotify:Templates:ComplianceDeclarationSubmissionDirectProducer:Subject", rows[0]["A"]);
        Assert.Equal("Subject", rows[0]["D"]);
        Assert.Equal("Certificate received", rows[0]["E"]);
        Assert.Equal("Tystysgrif wedi dod i law", rows[0]["F"]);
        Assert.DoesNotContain("G", rows[0].Keys);

        Assert.Equal("GovukNotify:Templates:ComplianceDeclarationSubmissionDirectProducer:Body", rows[1]["A"]);
        Assert.Equal("Body", rows[1]["D"]);
        Assert.Equal(englishBody, rows[1]["E"]);
        Assert.Equal("Welsh body", rows[1]["F"]);
        Assert.DoesNotContain("G", rows[1].Keys);
        AssertBodyRowIsTallerThanSubjectRow(workbookPath);

        using var textExport = JsonDocument.Parse(
            await File.ReadAllTextAsync(textExportPath, TestContext.Current.CancellationToken)
        );
        var textExportRows = textExport.RootElement.GetProperty("rows");

        Assert.Equal(2, textExport.RootElement.GetProperty("translatorNotes").GetArrayLength());
        Assert.Equal(
            "GovukNotify:Templates:ComplianceDeclarationSubmissionDirectProducer:Subject",
            textExportRows[0].GetProperty("translationKey").GetString()
        );
        Assert.Equal("Certificate received", textExportRows[0].GetProperty("english").GetString());
        Assert.Equal("Tystysgrif wedi dod i law", textExportRows[0].GetProperty("welsh").GetString());
        Assert.Equal(
            "GovukNotify:Templates:ComplianceDeclarationSubmissionDirectProducer:Body",
            textExportRows[1].GetProperty("translationKey").GetString()
        );
        Assert.Equal(englishBody, textExportRows[1].GetProperty("english").GetString());
    }

    [Fact]
    public async Task ExportAsync_WhenWelshMatchesEnglish_LeavesWelshCellBlank()
    {
        WriteAppSettings(
            """
            {
              "GovukNotify": {
                "Templates": {
                  "ExampleEmail": {
                    "TemplateId": {
                      "En": "en-template-id",
                      "Cy": "cy-template-id"
                    }
                  }
                }
              }
            }
            """
        );

        var reader = new FakeTemplateReader(
            new EmailTemplateContent("en-template-id", "Same subject", "Same body"),
            new EmailTemplateContent("cy-template-id", "Same subject", "Same body")
        );

        await new ExportService(reader).ExportAsync(_projectRoot, "src/Api/appsettings.json", "exports");

        var workbookPath = Path.Combine(_projectRoot, "exports", "01-example-email.xlsx");
        var rows = ReadWorkbookRows(workbookPath);

        Assert.Equal(string.Empty, rows[0]["F"]);
        Assert.Equal(string.Empty, rows[1]["F"]);
    }

    [Fact]
    public async Task ExportAsync_WhenExportedContentIsUnchanged_DoesNotRewriteWorkbookOrTextExport()
    {
        WriteAppSettings(
            """
            {
              "GovukNotify": {
                "Templates": {
                  "ExampleEmail": {
                    "TemplateId": {
                      "En": "en-template-id",
                      "Cy": "cy-template-id"
                    }
                  }
                }
              }
            }
            """
        );

        var reader = new FakeTemplateReader(
            new EmailTemplateContent("en-template-id", "Subject", "Body"),
            new EmailTemplateContent("cy-template-id", "Pwnc", "Corff")
        );

        await new ExportService(reader).ExportAsync(_projectRoot, "src/Api/appsettings.json", "exports");

        var workbookPath = Path.Combine(_projectRoot, "exports", "01-example-email.xlsx");
        var textExportPath = Path.Combine(_projectRoot, "exports", "json", "01-example-email.json");
        var originalWorkbook = await File.ReadAllBytesAsync(workbookPath, TestContext.Current.CancellationToken);
        var originalTextExport = await File.ReadAllTextAsync(textExportPath, TestContext.Current.CancellationToken);
        var preservedWriteTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(workbookPath, preservedWriteTime);
        File.SetLastWriteTimeUtc(textExportPath, preservedWriteTime);

        var consoleOutput = await CaptureConsoleOutputAsync(() =>
            new ExportService(reader).ExportAsync(_projectRoot, "src/Api/appsettings.json", "exports")
        );

        Assert.Equal(preservedWriteTime, File.GetLastWriteTimeUtc(workbookPath));
        Assert.Equal(preservedWriteTime, File.GetLastWriteTimeUtc(textExportPath));
        Assert.Equal(
            originalWorkbook,
            await File.ReadAllBytesAsync(workbookPath, TestContext.Current.CancellationToken)
        );
        Assert.Equal(
            originalTextExport,
            await File.ReadAllTextAsync(textExportPath, TestContext.Current.CancellationToken)
        );
        Assert.Contains("workbook: unchanged; JSON: unchanged", consoleOutput, StringComparison.Ordinal);
        Assert.Contains("Workbooks: created 0, updated 0, unchanged 1", consoleOutput, StringComparison.Ordinal);
        Assert.Contains("JSON sidecars: created 0, updated 0, unchanged 1", consoleOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportAsync_WhenTextExportIsMissing_CreatesItWithoutRewritingMatchingWorkbook()
    {
        WriteAppSettings(
            """
            {
              "GovukNotify": {
                "Templates": {
                  "ExampleEmail": {
                    "TemplateId": {
                      "En": "en-template-id",
                      "Cy": "cy-template-id"
                    }
                  }
                }
              }
            }
            """
        );

        var reader = new FakeTemplateReader(
            new EmailTemplateContent("en-template-id", "Subject", "Body"),
            new EmailTemplateContent("cy-template-id", "Pwnc", "Corff")
        );

        await new ExportService(reader).ExportAsync(_projectRoot, "src/Api/appsettings.json", "exports");

        var workbookPath = Path.Combine(_projectRoot, "exports", "01-example-email.xlsx");
        var textExportPath = Path.Combine(_projectRoot, "exports", "json", "01-example-email.json");
        File.Delete(textExportPath);
        var preservedWriteTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(workbookPath, preservedWriteTime);

        var consoleOutput = await CaptureConsoleOutputAsync(() =>
            new ExportService(reader).ExportAsync(_projectRoot, "src/Api/appsettings.json", "exports")
        );

        using var textExport = JsonDocument.Parse(
            await File.ReadAllTextAsync(textExportPath, TestContext.Current.CancellationToken)
        );
        var row = textExport.RootElement.GetProperty("rows")[0];

        Assert.Equal(preservedWriteTime, File.GetLastWriteTimeUtc(workbookPath));
        Assert.Contains("workbook: unchanged; JSON: created", consoleOutput, StringComparison.Ordinal);
        Assert.Equal("GovukNotify:Templates:ExampleEmail:Subject", row.GetProperty("translationKey").GetString());
        Assert.Equal("ExampleEmail", row.GetProperty("templateName").GetString());
        Assert.Equal("Subject", row.GetProperty("english").GetString());
        Assert.Equal("Pwnc", row.GetProperty("welsh").GetString());
    }

    [Fact]
    public async Task ExportAsync_WhenNoEnglishTemplateIdConfigured_Fails()
    {
        WriteAppSettings(
            """
            {
              "GovukNotify": {
                "Templates": {
                  "ExampleEmail": {
                    "TemplateId": {
                      "Cy": "cy-template-id"
                    }
                  }
                }
              }
            }
            """
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ExportService(new FakeTemplateReader()).ExportAsync(_projectRoot, "src/Api/appsettings.json", "exports")
        );

        Assert.Contains(
            "GovukNotify:Templates:ExampleEmail:TemplateId:En is missing",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    private static void AssertWorkbookRelationshipsUsePackageNamespace(string workbookPath)
    {
        using var archive = ZipFile.OpenRead(workbookPath);
        var relationshipsEntry =
            archive.GetEntry("xl/_rels/workbook.xml.rels")
            ?? throw new InvalidOperationException("Workbook is missing xl/_rels/workbook.xml.rels.");

        using var stream = relationshipsEntry.Open();
        var document = XDocument.Load(stream);

        Assert.Equal("http://schemas.openxmlformats.org/package/2006/relationships", document.Root!.Name.NamespaceName);
    }

    private static void AssertTranslatorNotes(string workbookPath)
    {
        var rows = ReadWorksheetRows(workbookPath);

        Assert.Equal("Translator notes", rows[0]["E"]);
        Assert.Equal(
            "Preserve GOV.UK Notify personalisation placeholders such as ((regulator)) and ((obligationYear)).",
            rows[1]["E"]
        );
        Assert.Equal("Preserve Markdown formatting, links, headings and blank lines.", rows[2]["E"]);

        var headerRow = rows.Single(row => row.TryGetValue("A", out var value) && value == "Translation key");
        Assert.Equal(["A", "B", "C", "D", "E", "F"], headerRow.Keys.Order(StringComparer.Ordinal));
    }

    private static void AssertBodyRowIsTallerThanSubjectRow(string workbookPath)
    {
        var rows = ReadWorksheetXmlRows(workbookPath);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var subjectRowHeight = GetRowHeight(rows.Single(row => GetCellValue(row, "D", ns) == "Subject"));
        var bodyRowHeight = GetRowHeight(rows.Single(row => GetCellValue(row, "D", ns) == "Body"));

        Assert.True(
            bodyRowHeight > subjectRowHeight,
            $"Expected body row height {bodyRowHeight} to exceed subject row height {subjectRowHeight}."
        );
    }

    private void WriteAppSettings(string content)
    {
        var apiDirectory = Path.Combine(_projectRoot, "src", "Api");
        Directory.CreateDirectory(apiDirectory);
        File.WriteAllText(Path.Combine(apiDirectory, "appsettings.json"), content);
    }

    private static IReadOnlyList<Dictionary<string, string>> ReadWorkbookRows(string workbookPath)
    {
        var rows = ReadWorksheetRows(workbookPath);
        var headerIndex = Array.FindIndex(
            rows,
            row => row.TryGetValue("A", out var value) && value == "Translation key"
        );
        return rows[(headerIndex + 1)..];
    }

    private static Dictionary<string, string>[] ReadWorksheetRows(string workbookPath)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return ReadWorksheetXmlRows(workbookPath)
            .Select(row =>
                row.Elements(ns + "c")
                    .ToDictionary(
                        cell => GetColumnName(cell.Attribute("r")!.Value),
                        cell => cell.Descendants(ns + "t").Single().Value,
                        StringComparer.Ordinal
                    )
            )
            .ToArray();
    }

    private static XElement[] ReadWorksheetXmlRows(string workbookPath)
    {
        using var archive = ZipFile.OpenRead(workbookPath);
        var worksheetEntry =
            archive.GetEntry("xl/worksheets/sheet1.xml")
            ?? throw new InvalidOperationException("Workbook is missing xl/worksheets/sheet1.xml.");

        using var stream = worksheetEntry.Open();
        var document = XDocument.Load(stream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return document.Root!.Element(ns + "sheetData")!.Elements(ns + "row").ToArray();
    }

    private static string? GetCellValue(XElement row, string column, XNamespace ns)
    {
        var cell = row.Elements(ns + "c").SingleOrDefault(cell => GetColumnName(cell.Attribute("r")!.Value) == column);

        return cell?.Descendants(ns + "t").SingleOrDefault()?.Value;
    }

    private static double GetRowHeight(XElement row)
    {
        var height =
            row.Attribute("ht")?.Value
            ?? throw new InvalidOperationException($"Row {row.Attribute("r")?.Value} does not have a height.");

        return double.Parse(height, CultureInfo.InvariantCulture);
    }

    private static string GetColumnName(string cellReference)
    {
        var columnName = new string(cellReference.TakeWhile(char.IsLetter).ToArray());
        return columnName;
    }

    private static async Task<string> CaptureConsoleOutputAsync(Func<Task> action)
    {
        var originalOutput = Console.Out;
        using var output = new StringWriter();

        try
        {
            Console.SetOut(output);
            await action();
        }
        finally
        {
            Console.SetOut(originalOutput);
        }

        return output.ToString();
    }

    private sealed class FakeTemplateReader(params EmailTemplateContent[] templates) : IGovukNotifyTemplateReader
    {
        private readonly Dictionary<string, EmailTemplateContent> _templates = templates.ToDictionary(
            template => template.Id,
            StringComparer.Ordinal
        );

        public List<string> RequestedTemplateIds { get; } = [];

        public Task<EmailTemplateContent> GetTemplateAsync(string templateId)
        {
            RequestedTemplateIds.Add(templateId);
            return Task.FromResult(_templates[templateId]);
        }
    }
}
