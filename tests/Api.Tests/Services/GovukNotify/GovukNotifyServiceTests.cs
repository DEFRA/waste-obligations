using AwesomeAssertions;
using Defra.WasteObligations.Api.Services.GovukNotify;
using Defra.WasteObligations.Api.Utils.Http;
using Defra.WasteObligations.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Notify.Interfaces;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Services.GovukNotify;

public class GovukNotifyServiceTests : WireMockTestBase
{
    private ServiceCollection Services { get; }
    private IAsyncNotificationClient NotificationClient { get; } = Substitute.For<IAsyncNotificationClient>();

    public GovukNotifyServiceTests(WireMockContext context)
        : base(context)
    {
        var config = new Dictionary<string, string?>
        {
            { $"{GovukNotifyOptions.SectionName}:ApiKey", "dummyapikey" },
            {
                $"{GovukNotifyOptions.SectionName}:Templates:{nameof(GovukNotifyOptions.TemplateName.ComplianceDeclarationSubmissionDirectProducer)}:TemplateId:En",
                "en_template_id"
            },
            {
                $"{GovukNotifyOptions.SectionName}:Templates:{nameof(GovukNotifyOptions.TemplateName.ComplianceDeclarationSubmissionDirectProducer)}:TemplateId:Cy",
                "cy_template_id"
            },
            { $"{GovukNotifyOptions.SectionName}:TotalRequestTimeout:Timeout", "00:00:40" },
            { $"{GovukNotifyOptions.SectionName}:AttemptTimeout:Timeout", "00:00:05" },
        };

        Services = [];
        Services.AddGovukNotify();
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(config).Build());
        Services.AddTransient<ProxyHttpMessageHandler>();
        Services.AddSingleton<Func<HttpClient, GovukNotifyOptions, IAsyncNotificationClient>>(
            (_, options) =>
            {
                options.ApiKey.Should().Be("dummyapikey");

                return NotificationClient;
            }
        );
    }

    [Fact]
    public async Task RequiredService_ShouldNotBeNull()
    {
        await using var sp = Services.BuildServiceProvider();

        var service = sp.GetService<IGovukNotifyService>();

        service.Should().NotBeNull();
    }

    [Theory]
    [InlineData("en", "en_template_id")]
    [InlineData("cy", "cy_template_id")]
    public async Task SendComplianceDeclarationSubmittedEmail_ShouldSend(string language, string expectedTemplateId)
    {
        await using var sp = Services.BuildServiceProvider();

        var service = sp.GetRequiredService<IGovukNotifyService>();

        await service.SendComplianceDeclarationSubmittedEmail(
            GovukNotifyOptions.TemplateName.ComplianceDeclarationSubmissionDirectProducer,
            ["email1@email.com", "email2@email.com"],
            new Dictionary<string, object> { { "key1", "value1" } },
            language
        );

        await NotificationClient
            .Received()
            .SendEmailAsync(
                "email1@email.com",
                expectedTemplateId,
                Arg.Is<Dictionary<string, object>>(x => x.Count == 1 && (string)x["key1"] == "value1")
            );
        await NotificationClient
            .Received()
            .SendEmailAsync(
                "email2@email.com",
                expectedTemplateId,
                Arg.Is<Dictionary<string, object>>(x => x.Count == 1 && (string)x["key1"] == "value1")
            );
    }

    [Fact]
    public async Task InvalidTemplateName_Throws()
    {
        var config = new Dictionary<string, string?>
        {
            { $"{GovukNotifyOptions.SectionName}:ApiKey", "dummyapikey" },
            { $"{GovukNotifyOptions.SectionName}:Templates:InvalidTemplateName:TemplateId:En", "en_template_id" },
        };

        ServiceCollection services = [];
        services.AddGovukNotify();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(config).Build());
        services.AddTransient<ProxyHttpMessageHandler>();
        services.AddSingleton<Func<HttpClient, GovukNotifyOptions, IAsyncNotificationClient>>(
            (_, _) => NotificationClient
        );

        await using var sp = services.BuildServiceProvider();

        // ReSharper disable once AccessToDisposedClosure
        var act = () => sp.GetRequiredService<IOptions<GovukNotifyOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }
}
