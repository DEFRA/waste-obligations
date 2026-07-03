namespace Translations.Models;

internal sealed record EmailTranslationExportData(
    IReadOnlyList<string> TranslatorNotes,
    IReadOnlyList<EmailTranslationRow> Rows
);
