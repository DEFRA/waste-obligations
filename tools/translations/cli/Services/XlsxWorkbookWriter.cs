using System.IO.Compression;
using System.Globalization;
using System.Text;
using Translations.Models;

namespace Translations.Services;

internal static class XlsxWorkbookWriter
{
    private const int TranslationColumnCharacterWidth = 70;
    private const double LineHeight = 15;
    private const double VerticalPadding = 6;
    private const double MinimumTranslationRowHeight = 48;

    private const string ContentTypesXmlContent =
        """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>
  <Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>
  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
  <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
</Types>
""";

    private const string RootRelationshipsXmlContent =
        """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
  <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>
</Relationships>
""";

    private const string WorkbookRelationshipsXmlContent =
        """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
</Relationships>
""";

    private const string WorkbookXmlContent =
        """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <sheets>
    <sheet name="Welsh translations" sheetId="1" r:id="rId1"/>
  </sheets>
</workbook>
""";

    private const string StylesXmlContent =
        """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <fonts count="3">
    <font><sz val="11"/><color theme="1"/><name val="Calibri"/><family val="2"/></font>
    <font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/><family val="2"/></font>
    <font><b/><sz val="14"/><color theme="1"/><name val="Calibri"/><family val="2"/></font>
  </fonts>
  <fills count="3">
    <fill><patternFill patternType="none"/></fill>
    <fill><patternFill patternType="gray125"/></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FF1D70B8"/><bgColor indexed="64"/></patternFill></fill>
  </fills>
  <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
  <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
  <cellXfs count="3">
    <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0" applyAlignment="1"><alignment vertical="top" wrapText="1"/></xf>
    <xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1" applyAlignment="1"><alignment vertical="top" wrapText="1"/></xf>
    <xf numFmtId="0" fontId="2" fillId="0" borderId="0" xfId="0" applyFont="1" applyAlignment="1"><alignment vertical="top" wrapText="1"/></xf>
  </cellXfs>
  <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
</styleSheet>
""";

    private const string AppPropertiesXmlContent =
        """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes">
  <Application>translations</Application>
</Properties>
""";

    public static async Task WriteAsync(
        string path,
        EmailTranslationGroup group,
        IReadOnlyList<string> translatorInstructions)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        await using var fileStream = File.Create(path);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create);
        var headerRowNumber = translatorInstructions.Count + 3;

        await AddEntryAsync(archive, "[Content_Types].xml", ContentTypesXmlContent);
        await AddEntryAsync(archive, "_rels/.rels", RootRelationshipsXmlContent);
        await AddEntryAsync(archive, "docProps/app.xml", AppPropertiesXmlContent);
        await AddEntryAsync(archive, "docProps/core.xml", CorePropertiesXml());
        await AddEntryAsync(archive, "xl/workbook.xml", WorkbookXmlContent);
        await AddEntryAsync(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationshipsXmlContent);
        await AddEntryAsync(archive, "xl/styles.xml", StylesXmlContent);
        await AddEntryAsync(archive, "xl/worksheets/sheet1.xml", WorksheetXml(group, translatorInstructions, headerRowNumber));
    }

    private static async Task AddEntryAsync(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        await using var stream = await entry.OpenAsync();
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await writer.WriteAsync(content);
    }

    private static string WorksheetXml(
        EmailTranslationGroup group,
        IReadOnlyList<string> translatorInstructions,
        int headerRowNumber)
    {
        var rows = new StringBuilder();
        var mergeCells = new StringBuilder();

        rows.Append(Row(1, Cell("E", 1, "Translator notes", style: 2)));
        mergeCells.Append("""<mergeCell ref="E1:F1"/>""");

        for (var index = 0; index < translatorInstructions.Count; index++)
        {
            var instructionRowNumber = index + 2;
            rows.Append(Row(instructionRowNumber, Cell("E", instructionRowNumber, translatorInstructions[index])));
            mergeCells.Append($"""<mergeCell ref="E{instructionRowNumber}:F{instructionRowNumber}"/>""");
        }

        rows.Append(Row(
            headerRowNumber,
            Cell("A", headerRowNumber, "Translation key", style: 1),
            Cell("B", headerRowNumber, "Template name", style: 1),
            Cell("C", headerRowNumber, "Template id", style: 1),
            Cell("D", headerRowNumber, "Field", style: 1),
            Cell("E", headerRowNumber, "English", style: 1),
            Cell("F", headerRowNumber, "Welsh", style: 1)));

        var rowNumber = headerRowNumber;
        foreach (var row in group.Rows)
        {
            rowNumber++;
            rows.Append(Row(
                rowNumber,
                CalculateTranslationRowHeight(row),
                Cell("A", rowNumber, row.TranslationKey),
                Cell("B", rowNumber, row.TemplateName),
                Cell("C", rowNumber, row.TemplateId),
                Cell("D", rowNumber, row.Field),
                Cell("E", rowNumber, row.English),
                Cell("F", rowNumber, row.Welsh)));
        }

        var finalRowNumber = Math.Max(rowNumber, headerRowNumber);
        var mergeCellsXml = mergeCells.Length == 0
            ? string.Empty
            : $"""<mergeCells count="{translatorInstructions.Count + 1}">{mergeCells}</mergeCells>""";

        return $$"""
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <dimension ref="A1:F{{finalRowNumber}}"/>
  <sheetViews>
    <sheetView workbookViewId="0">
      <pane ySplit="{{headerRowNumber}}" topLeftCell="A{{headerRowNumber + 1}}" activePane="bottomLeft" state="frozen"/>
    </sheetView>
  </sheetViews>
  <sheetFormatPr defaultRowHeight="15"/>
  <cols>
    <col min="1" max="4" width="28" customWidth="1" hidden="1"/>
    <col min="5" max="6" width="70" customWidth="1"/>
  </cols>
  <sheetData>
{{rows}}
  </sheetData>
  <autoFilter ref="A{{headerRowNumber}}:F{{finalRowNumber}}"/>
  {{mergeCellsXml}}
  <pageMargins left="0.7" right="0.7" top="0.75" bottom="0.75" header="0.3" footer="0.3"/>
</worksheet>
""";
    }

    private static double CalculateTranslationRowHeight(EmailTranslationRow row)
    {
        var visibleLineCount = Math.Max(
            EstimateWrappedLineCount(row.English),
            EstimateWrappedLineCount(row.Welsh));

        return Math.Max(MinimumTranslationRowHeight, visibleLineCount * LineHeight + VerticalPadding);
    }

    private static int EstimateWrappedLineCount(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 1;
        }

        return value
            .ReplaceLineEndings("\n")
            .Split('\n')
            .Sum(line => Math.Max(1, (int)Math.Ceiling(line.Length / (double)TranslationColumnCharacterWidth)));
    }

    private static string Row(int rowNumber, params string[] cells) =>
        Row(rowNumber, rowNumber == 1 ? 24 : 48, cells);

    private static string Row(int rowNumber, double height, params string[] cells) =>
        $"""    <row r="{rowNumber}" ht="{height.ToString("0.##", CultureInfo.InvariantCulture)}" customHeight="1">{string.Concat(cells)}</row>""" + Environment.NewLine;

    private static string Cell(string column, int row, string? value, int style = 0)
    {
        var styleAttribute = style > 0 ? $""" s="{style}" """ : " ";
        return $"""<c r="{column}{row}"{styleAttribute}t="inlineStr"><is><t xml:space="preserve">{Escape(value ?? string.Empty)}</t></is></c>""";
    }

    private static string Escape(string value) =>
        value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);

    private static string CorePropertiesXml() =>
        $$"""
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:dcmitype="http://purl.org/dc/dcmitype/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <dc:creator>waste-obligations translations tool</dc:creator>
  <cp:lastModifiedBy>waste-obligations translations tool</cp:lastModifiedBy>
  <dcterms:created xsi:type="dcterms:W3CDTF">{{DateTime.UtcNow:O}}</dcterms:created>
  <dcterms:modified xsi:type="dcterms:W3CDTF">{{DateTime.UtcNow:O}}</dcterms:modified>
</cp:coreProperties>
""";
}
