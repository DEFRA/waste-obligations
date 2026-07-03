using AwesomeAssertions;
using Notify.Client;

namespace Defra.WasteObligations.Api.IntegrationTests.Scenarios;

public class GovukNotifyTests(ITestOutputHelper testOutputHelper) : IntegrationTestBase
{
    [Theory]
    [InlineData(GovukNotifyTemplateIds.ComplianceDeclarationSubmissionDirectProducerEnglish)]
    [InlineData(GovukNotifyTemplateIds.ComplianceDeclarationSubmissionDirectProducerWelsh)]
    public async Task DirectProducerSubmissionEmail_ShouldRender(string templateId)
    {
        var preview = await GenerateTemplatePreview(templateId);
        if (preview is null)
            return;

        preview.Value.Body.Should().Contain("The Regulator has received your 2026 certificate of compliance.");
        preview
            .Value.Body.Should()
            .Contain(
                "Contact the Regulator if you need to discuss your certificate of compliance: regulator@email.com."
            );
    }

    [Theory]
    [InlineData(GovukNotifyTemplateIds.ComplianceDeclarationSubmissionComplianceSchemeEnglish)]
    [InlineData(GovukNotifyTemplateIds.ComplianceDeclarationSubmissionComplianceSchemeWelsh)]
    public async Task ComplianceSchemeSubmissionEmail_ShouldRender(string templateId)
    {
        var preview = await GenerateTemplatePreview(templateId);
        if (preview is null)
            return;

        preview.Value.Subject.Should().Contain("2026");
        preview.Value.Subject.Should().Contain("Regulator");
        preview.Value.Body.Should().Contain("2026");
        preview.Value.Body.Should().Contain("Regulator");
        preview.Value.Body.Should().Contain("regulator@email.com");
        preview.Value.Body.Should().Contain("Submitter Name");
    }

    private async Task<(string Body, string Subject)?> GenerateTemplatePreview(string templateId)
    {
        var apiKey = Environment.GetEnvironmentVariable("GOVUKNOTIFY_APIKEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            testOutputHelper.WriteLine("GOVUKNOTIFY_APIKEY is not set");

            return null;
        }

        testOutputHelper.WriteLine("GOVUKNOTIFY_APIKEY found, running test");

        var notificationClient = new NotificationClient(apiKey);
        var personalisation = new Dictionary<string, object>
        {
            { "obligationYear", 2026 },
            { "regulator", "Regulator" },
            { "regulatorEmail", "regulator@email.com" },
            { "user", "Submitter Name" },
        };

        var preview = await notificationClient.GenerateTemplatePreviewAsync(templateId, personalisation);

        return (preview.body, preview.subject);
    }
}
