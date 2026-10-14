using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.OrganisationEligibility;
using Defra.WasteObligations.Api.Services.OrganisationObligations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Endpoints.Admin;

public class GetUnsubmittedPollingConfigurationTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddSingleton<IOptions<OrganisationEligibilityOptions>>(
            Options.Create(
                new OrganisationEligibilityOptions { RefreshPollingEnabled = true, RefreshPollIntervalSeconds = 300 }
            )
        );
        services.AddSingleton<IOptions<OrganisationObligationHydrationOptions>>(
            Options.Create(
                new OrganisationObligationHydrationOptions
                {
                    PollingEnabled = true,
                    RefreshInterval = TimeSpan.FromMinutes(5).Add(TimeSpan.FromMilliseconds(500)),
                    MaximumSummaryStaleness = TimeSpan.FromMinutes(30).Add(TimeSpan.FromMilliseconds(500)),
                    MaxDownstreamRequestsPerMinute = 180,
                    PollIntervalSeconds = 15,
                }
            )
        );
        var yearProvider = Substitute.For<ICurrentObligationYearProvider>();
        yearProvider.GetCurrentObligationYear().Returns(2026);
        services.AddSingleton(yearProvider);
    }

    [Fact]
    public async Task WhenAdmin_ShouldReturnEffectiveConfiguration()
    {
        var client = CreateClient(testUser: TestUser.Admin);
        var response = await client.GetAsync(
            Testing.Endpoints.Admin.UnsubmittedPollingConfiguration(),
            TestContext.Current.CancellationToken
        );
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WhenUnauthenticated_ShouldBeUnauthorized()
    {
        var client = CreateClient(addAuthorizationHeader: false);
        var response = await client.GetAsync(
            Testing.Endpoints.Admin.UnsubmittedPollingConfiguration(),
            TestContext.Current.CancellationToken
        );
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task WhenWriteOnly_ShouldBeForbidden()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);
        var response = await client.GetAsync(
            Testing.Endpoints.Admin.UnsubmittedPollingConfiguration(),
            TestContext.Current.CancellationToken
        );
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task WhenReadOnly_ShouldBeForbidden()
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);
        var response = await client.GetAsync(
            Testing.Endpoints.Admin.UnsubmittedPollingConfiguration(),
            TestContext.Current.CancellationToken
        );
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task WhenPublicPath_ShouldNotBeAvailable()
    {
        var client = CreateClient(testUser: TestUser.Admin);
        var response = await client.GetAsync(
            "/compliance-declarations/unsubmitted/polling-configuration",
            TestContext.Current.CancellationToken
        );
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }
}
