namespace Translations.Models;

internal sealed record EmailTranslationGroup(
    string Id,
    string FileName,
    string TemplateName,
    string EnglishTemplateId,
    string? WelshTemplateId,
    IReadOnlyList<EmailTranslationRow> Rows);
