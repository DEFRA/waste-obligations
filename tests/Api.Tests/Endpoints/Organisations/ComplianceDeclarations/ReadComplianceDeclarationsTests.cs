using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Dtos;
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

    [Theory]
    [InlineData(0)]
    public async Task Validation_WhenPageInvalid_ShouldBeBadRequest(int page)
    {
        var content = await RequestShouldBeBadRequest(EndpointQuery.New.Where(EndpointFilter.Page(page)));

        await VerifyJson(content);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Validation_WhenPageSizeInvalid_ShouldBeBadRequest(int pageSize)
    {
        var content = await RequestShouldBeBadRequest(EndpointQuery.New.Where(EndpointFilter.PageSize(pageSize)));

        await VerifyJson(content);
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

    [Fact]
    public async Task WhenPaging_ShouldReturnRequestedPage()
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);
        var query = EndpointQuery
            .New.Where(EndpointFilter.ObligationYear(FakeWasteOrganisationsService.Year))
            .Where(EndpointFilter.Page(2))
            .Where(EndpointFilter.PageSize(1));

        var response = await client.GetFromJsonAsync<ComplianceDeclarationsPaged>(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Read(
                FakeWasteOrganisationsService.OrganisationId,
                query
            ),
            TestContext.Current.CancellationToken
        );

        response.Should().NotBeNull();
        response.ComplianceDeclarations.Should().ContainSingle();
        response.Total.Should().Be(2);
        response.Page.Should().Be(2);
        response.PageSize.Should().Be(1);
    }

    private async Task<string> RequestShouldBeBadRequest(EndpointQuery query)
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Read(
                FakeWasteOrganisationsService.OrganisationId,
                query
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }
}
