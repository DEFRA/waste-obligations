using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Endpoints.Admin;

public class ReadUnsubmittedPollingPlanTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    private IUnsubmittedPollingPlanService PollingPlanService { get; } =
        Substitute.For<IUnsubmittedPollingPlanService>();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddTransient<IUnsubmittedPollingPlanService>(_ => PollingPlanService);
    }

    [Fact]
    public async Task WhenAdmin_ShouldReturnPollingPlan()
    {
        PollingPlanService.Get(Arg.Any<CancellationToken>()).Returns(Plan());
        var client = CreateClient(testUser: TestUser.Admin);

        var response = await client.GetAsync(
            Testing.Endpoints.Admin.UnsubmittedPollingPlan(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WhenReadWriteUser_ShouldBeForbidden()
    {
        var client = CreateClient();

        var response = await client.GetAsync(
            Testing.Endpoints.Admin.UnsubmittedPollingPlan(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task WhenUnauthenticated_ShouldBeUnauthorized()
    {
        var client = CreateClient(addAuthorizationHeader: false);

        var response = await client.GetAsync(
            Testing.Endpoints.Admin.UnsubmittedPollingPlan(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    private static UnsubmittedPollingPlan Plan() =>
        new()
        {
            CurrentObligationYear = 2026,
            TargetFullRefreshMinutes = 30,
            SafetyCeilingRequestsPerMinute = 20,
            RecommendedRateHeadroomPercentage = 20,
            SourceReadSucceeded = true,
            SourceOrganisationCount = 4245,
            Warnings = ["Potential hydration organisation counts exclude Account reference resolution."],
            Years =
            [
                new UnsubmittedPollingPlanYear
                {
                    ObligationYear = 2025,
                    Classification = "HistoricalBackfill",
                    PotentialHydrationOrganisationCount = 24,
                    RegisteredRegistrationCount = 25,
                    RequiredRequestsPerMinute = 1,
                    RecommendedRequestsPerMinute = 2,
                    EstimatedFullRefreshMinutesAtSafetyCeiling = 1.2,
                },
                new UnsubmittedPollingPlanYear
                {
                    ObligationYear = 2026,
                    Classification = "Current",
                    PotentialHydrationOrganisationCount = 4245,
                    RegisteredRegistrationCount = 4250,
                    RequiredRequestsPerMinute = 142,
                    RecommendedRequestsPerMinute = 171,
                    EstimatedFullRefreshMinutesAtSafetyCeiling = 212.25,
                },
            ],
        };
}
