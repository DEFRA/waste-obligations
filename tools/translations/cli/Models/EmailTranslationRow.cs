namespace Translations.Models;

internal sealed record EmailTranslationRow(
    string TranslationKey,
    string TemplateName,
    string TemplateId,
    string Field,
    string English,
    string Welsh);
