using Notify.Client;
using Notify.Interfaces;
using Translations.Models;

namespace Translations.Services;

internal sealed class GovukNotifyTemplateReader : IGovukNotifyTemplateReader
{
    private readonly IAsyncNotificationClient _client;

    public GovukNotifyTemplateReader(string apiKey)
        : this(new NotificationClient(apiKey))
    {
    }

    internal GovukNotifyTemplateReader(IAsyncNotificationClient client)
    {
        _client = client;
    }

    public async Task<EmailTemplateContent> GetTemplateAsync(string templateId)
    {
        var template = await _client.GetTemplateByIdAsync(templateId);

        return new EmailTemplateContent(
            template.id,
            NormalizeLineEndings(template.subject ?? string.Empty),
            NormalizeLineEndings(template.body ?? string.Empty));
    }

    private static string NormalizeLineEndings(string value) =>
        value.ReplaceLineEndings("\n");
}
