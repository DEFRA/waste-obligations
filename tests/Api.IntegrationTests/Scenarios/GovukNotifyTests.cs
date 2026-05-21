using AwesomeAssertions;
using Notify.Client;

namespace Defra.WasteObligations.Api.IntegrationTests.Scenarios;

public class GovukNotifyTests(ITestOutputHelper testOutputHelper) : IntegrationTestBase
{
    [Theory]
    [InlineData("5f64e3bd-d454-4a45-a9c6-9409bf940d7a")]
    [InlineData("b3223b0b-a467-40c1-9150-f78b76d11fd8")]
    public async Task SubmissionEmail_ShouldRender(string templateId)
    {
        var apiKey = Environment.GetEnvironmentVariable("GOVUKNOTIFY_APIKEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            testOutputHelper.WriteLine("GOVUKNOTIFY_APIKEY is not set");
            return;
        }

        testOutputHelper.WriteLine("GOVUKNOTIFY_APIKEY found, running test");

        var notificationClient = new NotificationClient(apiKey);

        var preview = await notificationClient.GenerateTemplatePreviewAsync(
            templateId,
            new Dictionary<string, object>
            {
                { "obligationYear", 2026 },
                { "regulator", "Regulator" },
                { "regulatorEmail", "regulator@email.com" },
            }
        );

        preview.body.Should().Contain("The Regulator has received your 2026 certificate of compliance.");
        preview
            .body.Should()
            .Contain(
                "Contact the Regulator if you need to discuss your certificate of compliance: regulator@email.com."
            );
    }
}
