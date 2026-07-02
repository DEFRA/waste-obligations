using Translations.Models;

namespace Translations.Services;

internal interface IGovukNotifyTemplateReader
{
    Task<EmailTemplateContent> GetTemplateAsync(string templateId);
}
