using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Endpoints.Admin;
using Defra.WasteObligations.Api.Services.Admin;
using Defra.WasteObligations.Testing;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Endpoints.Admin;

public class ReadUnsubmittedReferenceResolutionIssuesTests(
    ApiWebApplicationFactory factory,
    ITestOutputHelper outputHelper
) : EndpointTestBase(factory, outputHelper)
{
    private IAdminDataService AdminDataService { get; } = Substitute.For<IAdminDataService>();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddTransient<IAdminDataService>(_ => AdminDataService);
    }

    [Fact]
    public async Task WhenAdmin_ShouldReturnReferenceResolutionIssues()
    {
        AdminDataService.ReadReferenceResolutionIssues(Arg.Any<CancellationToken>()).Returns(Stream(Issue()));
        var client = CreateClient(testUser: TestUser.Admin);

        var response = await client.GetAsync(
            Testing.Endpoints.Admin.UnsubmittedReferenceResolutionIssues(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be(EntityStreamResult<object>.ContentType);
        await VerifyJson(AsJsonArray(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
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

    private static OrganisationComplianceDeclarationEligibility Issue() =>
        new()
        {
            Id = ObjectId.Parse("68bc00000000000000000003"),
            Generation = "generation",
            OrganisationId = Guid.Parse("3a67e998-ebd2-4dcc-9982-a12ef6db10a5"),
            ObligationYear = 2026,
            RegistrationType = Defra.WasteObligations.Api.Data.Entities.RegistrationType.ComplianceScheme,
            RegistrationStatus = OrganisationRegistrationStatus.Registered,
            Name = "Example scheme",
            TradingName = "Example trading name",
            CompaniesHouseNumber = "01234567",
            BusinessCountry = "GB-ENG",
            ReferenceNumber = "100001",
            ReferenceNumberResolutionState = OrganisationReferenceNumberResolutionState.NotFound,
            DeclarationStateUpdatedAt = new DateTime(2026, 9, 6, 9, 0, 0, DateTimeKind.Utc),
            SourceFingerprint = "fingerprint",
            RefreshedAt = new DateTime(2026, 9, 6, 9, 1, 0, DateTimeKind.Utc),
        };

    private static string AsJsonArray(string content) => $"[{content.Trim()}]";

    private static async IAsyncEnumerable<T> Stream<T>(params T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
    }
}
