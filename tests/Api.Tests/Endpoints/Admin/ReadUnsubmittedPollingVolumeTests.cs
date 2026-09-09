using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Endpoints.Admin;

public class ReadUnsubmittedPollingVolumeTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    private IUnsubmittedPollingVolumeService PollingVolumeService { get; } =
        Substitute.For<IUnsubmittedPollingVolumeService>();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddTransient<IUnsubmittedPollingVolumeService>(_ => PollingVolumeService);
    }

    [Fact]
    public async Task WhenAdmin_ShouldReturnPollingVolume()
    {
        PollingVolumeService.Get(Arg.Any<CancellationToken>()).Returns(Volume());
        var client = CreateClient(testUser: TestUser.Admin);

        var response = await client.GetAsync(
            Testing.Endpoints.Admin.UnsubmittedPollingVolume(),
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
            Testing.Endpoints.Admin.UnsubmittedPollingVolume(),
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
            Testing.Endpoints.Admin.UnsubmittedPollingVolume(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    private static UnsubmittedPollingVolume Volume() =>
        new()
        {
            CurrentObligationYear = 2026,
            SourceOrganisationCount = 4245,
            Years =
            [
                new UnsubmittedPollingVolumeYear
                {
                    ObligationYear = 2025,
                    IsCurrentObligationYear = false,
                    RegisteredOrganisationCount = 3,
                    RegisteredRegistrationCount = 4,
                },
                new UnsubmittedPollingVolumeYear
                {
                    ObligationYear = 2026,
                    IsCurrentObligationYear = true,
                    RegisteredOrganisationCount = 4245,
                    RegisteredRegistrationCount = 4250,
                },
            ],
        };
}
