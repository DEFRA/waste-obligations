using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Testing.Fixtures.Dtos;
using Defra.WasteObligations.Testing.Fixtures.Entities;
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
        const string regulatorName = "Regulator";
        const string regulatorEmail = "regulator@email.com";
        const string firstName = "First";
        const string lastName = "Last";
        var complianceDeclaration = ComplianceDeclarationFixture.DirectProducer().Create();
        var submissionDeadlineYearText = (complianceDeclaration.ObligationYear + 1).ToString();
        var callerParameters = NotificationFixture.DirectProducerCancellationParameters(regulatorName);

        var personalisation = ComplianceDeclarationCancellationNotificationParameters.Build(
            complianceDeclaration,
            callerParameters
        );
        personalisation["firstName"] = firstName;
        personalisation["lastName"] = lastName;

        var preview = await GenerateTemplatePreview(templateId, personalisation);
        if (preview is null)
            return;

        preview.Value.Body.Should().Contain(submissionDeadlineYearText);
        preview.Value.Body.Should().Contain(regulatorName);
        preview.Value.Body.Should().Contain(regulatorEmail);
        (
            preview.Value.Body.Contains(callerParameters["certOrStatement"])
            || preview.Value.Body.Contains(callerParameters["certOrStatement_cy"])
        )
            .Should()
            .BeTrue();
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
