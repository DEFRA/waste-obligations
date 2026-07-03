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
        var body = await GenerateTemplatePreviewBody(templateId);
        if (body is null)
            return;

        body.Should().Contain("The Regulator has received your 2026 certificate of compliance.");
        body.Should()
            .Contain(
                "Contact the Regulator if you need to discuss your certificate of compliance: regulator@email.com."
            );
    }

    [Fact]
    public async Task ComplianceSchemeEnglishSubmissionEmail_ShouldRender()
    {
        var body = await GenerateTemplatePreviewBody(
            GovukNotifyTemplateIds.ComplianceDeclarationSubmissionComplianceSchemeEnglish
        );
        if (body is null)
            return;

        body.Should().Contain("2026");
        body.Should().Contain("Regulator");
        body.Should().Contain("regulator@email.com");
        body.Should().Contain("Submitter Name");
    }

    [Fact]
    public async Task ComplianceSchemeWelshSubmissionEmail_ShouldRender()
    {
        var body = await GenerateTemplatePreviewBody(
            GovukNotifyTemplateIds.ComplianceDeclarationSubmissionComplianceSchemeWelsh
        );
        if (body is null)
            return;

        body.Should().Contain("2026");
        body.Should().Contain("Regulator");
        body.Should().Contain("regulator@email.com");
        body.Should().Contain("Submitter Name");
    }

    private async Task<string?> GenerateTemplatePreviewBody(string templateId)
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

        return preview.body;
    }
}
