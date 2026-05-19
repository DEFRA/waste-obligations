using Microsoft.Extensions.Options;
using Notify.Interfaces;

namespace Defra.WasteObligations.Api.Services.GovukNotify;

public class GovukNotifyService(
    HttpClient httpClient,
    IOptions<GovukNotifyOptions> options,
    Func<HttpClient, GovukNotifyOptions, IAsyncNotificationClient> notificationClientFactory
) : IGovukNotifyService
{
    public async Task SendComplianceDeclarationSubmittedEmail(
        GovukNotifyOptions.TemplateName template,
        IEnumerable<string> recipients,
        Dictionary<string, object> personalisation,
        string language
    )
    {
        var client = notificationClientFactory(httpClient, options.Value);
        var templateId = options.Value.Templates[template].GetTemplateId(language);

        await Task.WhenAll(recipients.Select(x => client.SendEmailAsync(x, templateId, personalisation)));
    }
}
