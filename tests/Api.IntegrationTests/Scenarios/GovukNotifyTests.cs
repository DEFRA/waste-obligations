using AwesomeAssertions;
using Defra.WasteObligations.Testing.Fixtures.Dtos;
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
            new Dictionary<string, object>
            {
                { "obligationYear", obligationYear },
                { "regulatorLeading", regulatorLeading },
                { "regulatorInline", regulatorInline },
                { "regulatorEmail", regulatorEmail },
                { "user", user },
            }
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

    [Theory]
    [InlineData(GovukNotifyTemplateIds.ComplianceDeclarationCancellationNotSignedByCorrectPersonEnglish)]
    [InlineData(GovukNotifyTemplateIds.ComplianceDeclarationCancellationNotSignedByCorrectPersonWelsh)]
    [InlineData(GovukNotifyTemplateIds.ComplianceDeclarationCancellationRecyclingObligationsChangedEnglish)]
    [InlineData(GovukNotifyTemplateIds.ComplianceDeclarationCancellationRecyclingObligationsChangedWelsh)]
    [InlineData(GovukNotifyTemplateIds.ComplianceDeclarationCancellationCanMeetRecyclingObligationsEnglish)]
    [InlineData(GovukNotifyTemplateIds.ComplianceDeclarationCancellationCanMeetRecyclingObligationsWelsh)]
    [InlineData(GovukNotifyTemplateIds.ComplianceDeclarationCancellationProducerRequestedEnglish)]
    [InlineData(GovukNotifyTemplateIds.ComplianceDeclarationCancellationProducerRequestedWelsh)]
    public async Task CancellationEmail_ShouldRenderPersonalisation(string templateId)
    {
        const int obligationYear = 2026;
        const string obligationYearText = "2026";
        const string regulatorName = "Regulator";
        const string regulatorEmail = "regulator@email.com";
        const string firstName = "First";
        const string lastName = "Last";
        var callerParameters = NotificationFixture.DirectProducerCancellationParameters(regulatorName);

        var personalisation = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            { "year", obligationYear },
            { "regulator", regulatorName },
            { "regulatorEmail", regulatorEmail },
            { "firstName", firstName },
            { "lastName", lastName },
        };

        foreach (var (key, value) in callerParameters)
            personalisation[key] = value;

        var preview = await GenerateTemplatePreview(templateId, personalisation);
        if (preview is null)
            return;

        preview.Value.Subject.Should().Contain(obligationYearText);
        preview.Value.Subject.Should().Contain(regulatorName);
        preview.Value.Body.Should().Contain(obligationYearText);
        preview.Value.Body.Should().Contain(regulatorName);
        preview.Value.Body.Should().Contain(regulatorEmail);
        preview.Value.Body.Should().Contain(callerParameters["certOrStatement"]);
        preview.Value.Body.Should().Contain(callerParameters["certOrStatement_cy"]);
        preview.Value.Body.Should().Contain(firstName);
        preview.Value.Body.Should().Contain(lastName);
    }

    private async Task<(string Body, string Subject)?> GenerateTemplatePreview(
        string templateId,
        Dictionary<string, object> personalisation
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
        var preview = await notificationClient.GenerateTemplatePreviewAsync(templateId, personalisation);

        testOutputHelper.WriteLine($"GOV.UK Notify template '{templateId}' rendered subject:");
        testOutputHelper.WriteLine(preview.subject);
        testOutputHelper.WriteLine($"GOV.UK Notify template '{templateId}' rendered body:");
        testOutputHelper.WriteLine(preview.body);

        return (preview.body, preview.subject);
    }
}
