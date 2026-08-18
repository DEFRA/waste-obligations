using AwesomeAssertions;
using Notify.Client;

namespace Defra.WasteObligations.Api.IntegrationTests.Scenarios;

public class GovukNotifyTests(ITestOutputHelper testOutputHelper) : IntegrationTestBase
{
    [Theory]
    [InlineData(GovukNotifyTemplateIds.ComplianceDeclarationSubmissionDirectProducerEnglish, false)]
    [InlineData(GovukNotifyTemplateIds.ComplianceDeclarationSubmissionDirectProducerWelsh, true)]
    [InlineData(GovukNotifyTemplateIds.ComplianceDeclarationSubmissionComplianceSchemeEnglish, false)]
    [InlineData(GovukNotifyTemplateIds.ComplianceDeclarationSubmissionComplianceSchemeWelsh, true)]
    public async Task SubmissionEmail_ShouldRenderPersonalisation(string templateId, bool isWales)
    {
        const int obligationYear = 2026;
        const string obligationYearText = "2026";
        const string regulatorName = "Regulator";
        const string regulatorEmail = "regulator@email.com";
        const string user = "Submitter Name";
        var regulatorLeading = isWales ? regulatorName : $"The {regulatorName}";
        var regulatorInline = isWales ? regulatorName : $"the {regulatorName}";

        var preview = await GenerateTemplatePreview(
            templateId,
            obligationYear,
            regulatorLeading,
            regulatorInline,
            regulatorEmail,
            user
        );
        if (preview is null)
            return;

        preview.Value.Subject.Should().Contain(obligationYearText);
        preview.Value.Subject.Should().Contain(regulatorName);
        preview.Value.Body.Should().Contain(obligationYearText);
        preview.Value.Body.Should().Contain(regulatorName);
        preview.Value.Body.Should().Contain(regulatorEmail);
        preview.Value.Body.Should().Contain(user);
    }

    private async Task<(string Body, string Subject)?> GenerateTemplatePreview(
        string templateId,
        int obligationYear,
        string regulatorLeading,
        string regulatorInline,
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
            { "regulatorLeading", regulatorLeading },
            { "regulatorInline", regulatorInline },
            { "regulatorEmail", regulatorEmail },
            { "user", user },
        };

        var preview = await notificationClient.GenerateTemplatePreviewAsync(templateId, personalisation);

        testOutputHelper.WriteLine($"GOV.UK Notify template '{templateId}' rendered subject:");
        testOutputHelper.WriteLine(preview.subject);
        testOutputHelper.WriteLine($"GOV.UK Notify template '{templateId}' rendered body:");
        testOutputHelper.WriteLine(preview.body);

        return (preview.body, preview.subject);
    }
}
