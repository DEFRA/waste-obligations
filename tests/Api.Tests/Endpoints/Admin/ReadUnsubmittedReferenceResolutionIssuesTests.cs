using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Endpoints.Admin;

public class ReadUnsubmittedReferenceResolutionIssuesTests(
    ApiWebApplicationFactory factory,
    ITestOutputHelper outputHelper
) : EndpointTestBase(factory, outputHelper)
{
    private IUnsubmittedReferenceResolutionIssuesService ReferenceResolutionIssuesService { get; } =
        Substitute.For<IUnsubmittedReferenceResolutionIssuesService>();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddTransient<IUnsubmittedReferenceResolutionIssuesService>(_ => ReferenceResolutionIssuesService);
    }

    [Fact]
    public async Task WhenAdmin_ShouldReturnReferenceResolutionIssues()
    {
        ReferenceResolutionIssuesService.Get(Arg.Any<CancellationToken>()).Returns(Issues());
        var client = CreateClient(testUser: TestUser.Admin);

        var response = await client.GetAsync(
            Testing.Endpoints.Admin.UnsubmittedReferenceResolutionIssues(),
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
            Testing.Endpoints.Admin.UnsubmittedReferenceResolutionIssues(),
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
            Testing.Endpoints.Admin.UnsubmittedReferenceResolutionIssues(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    private static UnsubmittedReferenceResolutionIssues Issues() =>
        new()
        {
            ActiveGeneration = "generation",
            ReferenceResolutionIssues =
            [
                new UnsubmittedReferenceResolutionIssue
                {
                    OrganisationId = Guid.Parse("3a67e998-ebd2-4dcc-9982-a12ef6db10a5"),
                    ObligationYear = 2026,
                    RegistrationType = RegistrationType.ComplianceScheme,
                    RegistrationStatus = "Registered",
                    Name = "Example scheme",
                    TradingName = "Example trading name",
                    CompaniesHouseNumber = "01234567",
                    Country = "GB-ENG",
                    ReferenceResolutionState = "NotFound",
                },
            ],
        };
}
