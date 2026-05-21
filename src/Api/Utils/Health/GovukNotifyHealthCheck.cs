using System.Diagnostics.CodeAnalysis;
using Defra.WasteObligations.Api.Services.GovukNotify;
using Defra.WasteObligations.Api.Utils.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Notify.Client;

namespace Defra.WasteObligations.Api.Utils.Health;

[ExcludeFromCodeCoverage]
public class GovukNotifyHealthCheck(IServiceProvider serviceProvider) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var proxyHandler = serviceProvider.GetRequiredService<ProxyHttpMessageHandler>();

            using var httpClient = new HttpClient(proxyHandler);

            var options = serviceProvider.GetRequiredService<IOptions<GovukNotifyOptions>>().Value;
            var asyncNotificationClient = serviceProvider.GetGovukNotifyFactory()(httpClient, options);
            var notificationClient =
                asyncNotificationClient as NotificationClient
                ?? throw new InvalidOperationException("Could not cast to NotificationClient");
            var retrieved = new List<string>();
            var failed = new List<string>();

            foreach (var template in options.Templates.Select(x => x.Value.TemplateId))
            {
                await GetTemplate(notificationClient, template.En, retrieved, failed);
                await GetTemplate(notificationClient, template.Cy, retrieved, failed);
            }

            var description = $"Connected to {notificationClient.BaseUrl}";
            var data = new Dictionary<string, object> { { nameof(retrieved), retrieved }, { nameof(failed), failed } };

            return failed.Count != 0
                ? HealthCheckResult.Degraded(description, data: data)
                : HealthCheckResult.Healthy(description, data);
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                exception: new Exception($"Failed to connect to {GovukNotifyOptions.SectionName}", ex)
            );
        }
    }

    private static async Task GetTemplate(
        NotificationClient notificationClient,
        string templateId,
        List<string> retrieved,
        List<string> failed
    )
    {
        try
        {
            var emailTemplate = await notificationClient.GetTemplateByIdAsync(templateId);
            retrieved.Add($"{emailTemplate.id} version {emailTemplate.version}");
        }
        catch (Exception)
        {
            failed.Add(templateId);
        }
    }
}
