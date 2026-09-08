using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Endpoints.Admin;

public class StartUnsubmittedHistoricalBackfillTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    private IUnsubmittedHistoricalBackfillService HistoricalBackfillService { get; } =
        Substitute.For<IUnsubmittedHistoricalBackfillService>();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddTransient<IUnsubmittedHistoricalBackfillService>(_ => HistoricalBackfillService);
    }

    [Fact]
    public async Task WhenAdmin_ShouldStartHistoricalBackfill()
    {
        HistoricalBackfillService.Start(Arg.Any<CancellationToken>()).Returns(Start());
        var client = CreateClient(testUser: TestUser.Admin);

        var response = await client.PostAsync(
            Testing.Endpoints.Admin.UnsubmittedHistoricalBackfill(),
            null,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WhenReadWriteUser_ShouldBeForbidden()
    {
        var client = CreateClient();

        var response = await client.PostAsync(
            Testing.Endpoints.Admin.UnsubmittedHistoricalBackfill(),
            null,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task WhenUnauthenticated_ShouldBeUnauthorized()
    {
        var client = CreateClient(addAuthorizationHeader: false);

        var response = await client.PostAsync(
            Testing.Endpoints.Admin.UnsubmittedHistoricalBackfill(),
            null,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    private static UnsubmittedHistoricalBackfillStart Start() =>
        new()
        {
            CurrentObligationYear = 2026,
            Years =
            [
                new UnsubmittedHistoricalBackfillYear
                {
                    ObligationYear = 2024,
                    PotentialHydrationOrganisationCount = 3,
                    Status = "Started",
                },
                new UnsubmittedHistoricalBackfillYear
                {
                    ObligationYear = 2025,
                    PotentialHydrationOrganisationCount = 42,
                    Status = "AlreadyCompleted",
                },
            ],
        };
}
