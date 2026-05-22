using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Defra.WasteObligations.Testing.Fakes;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;

namespace Defra.WasteObligations.Api.Tests.Endpoints.Organisations.ComplianceDeclarations;

public class ReadComplianceDeclarationTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    private FakeComplianceDeclarationService ComplianceDeclarationService { get; } = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddTransient<IWasteOrganisationsService>(_ => new FakeWasteOrganisationsService());
        services.AddTransient<IComplianceDeclarationService>(_ => ComplianceDeclarationService);
    }

    [Fact]
    public async Task WhenOrganisationNotFound_ShouldBeNotFound()
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Read(
                Guid.NewGuid(),
                ObjectId.GenerateNewId().ToString()
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WhenWriteOnlyUser_ShouldBeForbidden()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Read(
                Guid.NewGuid(),
                ObjectId.GenerateNewId().ToString()
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task WhenOrganisationFound_ButComplianceDeclarationNotFound_ShouldBeNotFound()
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Read(
                FakeWasteOrganisationsService.OrganisationId,
                ObjectId.GenerateNewId().ToString()
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WhenOrganisationFound_ButOrganisationDoesNotMatch_ShouldBeNotFound()
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Read(
                FakeWasteOrganisationsService.OrganisationId,
                FakeComplianceDeclarationService.NonMatchingOrganisationComplianceDeclarationId.ToString()
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WhenFound_AndMatch_ShouldBeOk()
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetStringAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Read(
                FakeWasteOrganisationsService.OrganisationId,
                FakeComplianceDeclarationService.ComplianceDeclarationId.ToString()
            ),
            TestContext.Current.CancellationToken
        );

        await VerifyJson(response).DontScrubGuids().DontScrubDateTimes();
    }
}
