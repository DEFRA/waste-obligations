using AwesomeAssertions;
using Notify.Client;

namespace Defra.WasteObligations.Api.IntegrationTests.Scenarios;

public class GovukNotifyTests(ITestOutputHelper testOutputHelper) : IntegrationTestBase
{
    private const string DirectProducerEnglishTemplateId = "5f64e3bd-d454-4a45-a9c6-9409bf940d7a";
    private const string DirectProducerWelshTemplateId = "b3223b0b-a467-40c1-9150-f78b76d11fd8";
    private const string ComplianceSchemeEnglishTemplateId = "b103685d-de8a-4ea9-abc8-d244ca26841b";
    private const string ComplianceSchemeWelshTemplateId = "95d8c2aa-a229-4ecb-9774-78ec06d0cbac";

    [Theory]
    [InlineData(DirectProducerEnglishTemplateId)]
    [InlineData(DirectProducerWelshTemplateId)]
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
        var body = await GenerateTemplatePreviewBody(ComplianceSchemeEnglishTemplateId);
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
        var body = await GenerateTemplatePreviewBody(ComplianceSchemeWelshTemplateId);
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
