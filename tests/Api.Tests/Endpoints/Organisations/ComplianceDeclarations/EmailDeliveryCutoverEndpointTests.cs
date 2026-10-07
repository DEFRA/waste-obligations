using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.GovukNotify;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Defra.WasteObligations.Testing.Fakes;
using Defra.WasteObligations.Testing.Fixtures.AccountBackend;
using Defra.WasteObligations.Testing.Fixtures.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using NSubstitute;
using ComplianceDeclaration = Defra.WasteObligations.Api.Data.Entities.ComplianceDeclaration;

namespace Defra.WasteObligations.Api.Tests.Endpoints.Organisations.ComplianceDeclarations;

public class EmailDeliveryCutoverEndpointTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    private const string CutoverUtc = "2026-04-26T14:00:00Z";
    private const string CutoverConfigurationKey = $"{EmailDeliveryOptions.SectionName}:EmailDeliveryCutoverUtc";
    private IGovukNotifyService GovukNotifyService { get; } = Substitute.For<IGovukNotifyService>();
    private ICancellationEmailRecipientResolver CancellationEmailRecipientResolver { get; } =
        Substitute.For<ICancellationEmailRecipientResolver>();

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, CutoverUtc)]
    public async Task WhenSubmitted_ShouldPreserveCreatedResponse(bool shouldSend, string? cutover)
    {
        using var configuredFactory = CreateFactory(cutover);
        using var client = CreateWriteClient(configuredFactory);

        var response = await client.PostAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Create(FakeWasteOrganisationsService.OrganisationId),
            CreateComplianceDeclarationRequestFixture
                .DirectProducer(FakeWasteOrganisationsService.OrganisationId)
                .Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        await GovukNotifyService
            .Received(shouldSend ? 1 : 0)
            .SendComplianceDeclarationSubmittedEmail(
                Arg.Any<GovukNotifyOptions.TemplateName>(),
                Arg.Any<IEnumerable<string>>(),
                Arg.Any<Dictionary<string, object>>(),
                Arg.Any<string>()
            );
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .UseParameters(shouldSend)
            .DontScrubGuids()
            .DontScrubDateTimes();
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, CutoverUtc)]
    public async Task WhenCancelled_ShouldPreserveOkResponse(bool shouldSend, string? cutover)
    {
        using var configuredFactory = CreateFactory(cutover);
        using var client = CreateWriteClient(configuredFactory);

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Update(
                FakeWasteOrganisationsService.OrganisationId,
                FakeComplianceDeclarationService.ComplianceDeclarationId.ToString()
            ),
            UpdateComplianceDeclarationRequestFixture.Cancelled().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await GovukNotifyService
            .Received(shouldSend ? 1 : 0)
            .SendComplianceDeclarationCancelledEmail(
                Arg.Any<GovukNotifyOptions.TemplateName>(),
                Arg.Any<IEnumerable<(string Email, Dictionary<string, object> Personalisation)>>(),
                Arg.Any<string>()
            );
        await CancellationEmailRecipientResolver
            .Received(shouldSend ? 1 : 0)
            .ResolveAsync(Arg.Any<ComplianceDeclaration>(), Arg.Any<Organisation>(), Arg.Any<CancellationToken>());
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .UseParameters(shouldSend)
            .DontScrubGuids()
            .DontScrubDateTimes();
    }

    [Theory]
    [InlineData("2026-04-26T14:00:00")]
    [InlineData("2026-04-26T14:00:00+01:00")]
    [InlineData("invalid")]
    public void WhenCutoverInvalid_ShouldRejectStartupConfiguration(string cutover)
    {
        using var configuredFactory = CreateFactory(null);
        var services = configuredFactory.Services;
        services.GetRequiredService<IConfiguration>()[CutoverConfigurationKey] = cutover;
        services.GetRequiredService<IOptionsMonitorCache<EmailDeliveryOptions>>().Clear();

        var act = () => services.GetRequiredService<IStartupValidator>().Validate();

        act.Should().Throw<OptionsValidationException>();
        GovukNotifyService.ReceivedCalls().Should().BeEmpty();
    }

    private WebApplicationFactory<Program> CreateFactory(string? cutover)
    {
        var timestamp = new DateTimeOffset(2026, 4, 26, 14, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(timestamp);
        var complianceDeclarationService = new FakeComplianceDeclarationService
        {
            UtcNow = () => timestamp,
            CreateNewId = () => ObjectId.Parse("6830b9d4c7e21f5a8d3e64b2"),
        };
        CancellationEmailRecipientResolver
            .ResolveAsync(Arg.Any<ComplianceDeclaration>(), Arg.Any<Organisation>(), Arg.Any<CancellationToken>())
            .Returns([PersonEmailFixture.Submitter()]);

        return Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration(configuration =>
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?> { [CutoverConfigurationKey] = cutover }
                )
            );
            builder.ConfigureTestServices(services =>
            {
                services.AddTransient<IWasteOrganisationsService>(_ => new FakeWasteOrganisationsService());
                services.AddTransient<IComplianceDeclarationService>(_ => complianceDeclarationService);
                services.AddTransient<TimeProvider>(_ => timeProvider);
                services.AddTransient<IGovukNotifyService>(_ => GovukNotifyService);
                services.AddTransient<ICancellationEmailRecipientResolver>(_ => CancellationEmailRecipientResolver);
            });
        });
    }

    private static HttpClient CreateWriteClient(WebApplicationFactory<Program> configuredFactory)
    {
        var client = configuredFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("IntegrationTest-ApiKey-Write:integration-test-write"))
        );

        return client;
    }
}
