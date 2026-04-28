using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Defra.WasteObligations.Testing;
using Defra.WasteObligations.Testing.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace Defra.WasteObligations.Api.Tests.Endpoints.Organisations.ComplianceDeclarations;

public class ReadComplianceDeclarationsTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    private FakeComplianceDeclarationService ComplianceDeclarationService { get; } = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddTransient<IWasteOrganisationsService>(_ => new FakeWasteOrganisationsService());
        services.AddTransient<IComplianceDeclarationService>(_ => ComplianceDeclarationService);
    }

    [Fact]
    public async Task WhenNotFound_ShouldBeNotFound()
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Read(
                Guid.NewGuid(),
                EndpointQuery.New.Where(EndpointFilter.ObligationYear(2026))
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
                EndpointQuery.New.Where(EndpointFilter.ObligationYear(2026))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task WhenFound_ShouldReturnComplianceDeclarations()
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetStringAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Read(
                FakeWasteOrganisationsService.OrganisationId,
                EndpointQuery.New.Where(EndpointFilter.ObligationYear(FakeWasteOrganisationsService.Year))
            ),
            TestContext.Current.CancellationToken
        );

        await VerifyJson(response).DontScrubGuids().DontScrubDateTimes().ScrubMembers("id");
    }
}
