using AwesomeAssertions;
using Notify.Client;

namespace Defra.WasteObligations.Api.IntegrationTests.Scenarios;

public class GovukNotifyTests(ITestOutputHelper testOutputHelper) : IntegrationTestBase
{
    [Theory]
    [InlineData(GovukNotifyTemplateIds.ComplianceDeclarationSubmissionDirectProducerEnglish)]
    [InlineData(GovukNotifyTemplateIds.ComplianceDeclarationSubmissionDirectProducerWelsh)]
    [InlineData(GovukNotifyTemplateIds.ComplianceDeclarationSubmissionComplianceSchemeEnglish)]
    [InlineData(GovukNotifyTemplateIds.ComplianceDeclarationSubmissionComplianceSchemeWelsh)]
    public async Task SubmissionEmail_ShouldRenderPersonalisation(string templateId)
    {
        const int obligationYear = 2026;
        const string obligationYearText = "2026";
        const string regulator = "Regulator";
        const string regulatorEmail = "regulator@email.com";
        const string user = "Submitter Name";

        var preview = await GenerateTemplatePreview(templateId, obligationYear, regulator, regulatorEmail, user);
        if (preview is null)
            return;

        preview.Value.Subject.Should().Contain(obligationYearText);
        preview.Value.Subject.Should().Contain(regulator);
        preview.Value.Body.Should().Contain(obligationYearText);
        preview.Value.Body.Should().Contain(regulator);
        preview.Value.Body.Should().Contain(regulatorEmail);
        preview.Value.Body.Should().Contain(user);
    }

    private async Task<(string Body, string Subject)?> GenerateTemplatePreview(
        string templateId,
        int obligationYear,
        string regulator,
        string regulatorEmail,
        string user
    )
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
            { "obligationYear", obligationYear },
            { "regulator", regulator },
            { "regulatorEmail", regulatorEmail },
            { "user", user },
        };

        var preview = await notificationClient.GenerateTemplatePreviewAsync(templateId, personalisation);

        return (preview.body, preview.subject);
    }
}
