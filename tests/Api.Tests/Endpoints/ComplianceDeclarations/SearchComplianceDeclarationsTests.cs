using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Testing;

namespace Defra.WasteObligations.Api.Tests.Endpoints.ComplianceDeclarations;

public class SearchComplianceDeclarationsTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    [Fact]
    public async Task WhenWriteOnlyUser_ShouldBeForbidden()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.ComplianceDeclarations.Search(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(ObligationYear.Minimum - 1)]
    [InlineData(ObligationYear.Maximum + 1)]
    public async Task Validation_WhenObligationYearInvalid_ShouldBeBadRequest(int obligationYear)
    {
        var content = await RequestShouldBeBadRequest(
            EndpointQuery.New.Where(EndpointFilter.ObligationYear(obligationYear))
        );

        await VerifyJson(content);
    }

    [Fact]
    public async Task Validation_WhenStatusUnknown_ShouldBeBadRequest()
    {
        var content = await RequestShouldBeBadRequest(EndpointQuery.New.Where(EndpointFilter.Status("unknown")));

        await VerifyJson(content);
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
    public async Task WhenValid_ShouldBeOk()
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.ComplianceDeclarations.Search(
                EndpointQuery
                    .New.Where(EndpointFilter.ObligationYear(2026))
                    .Where(
                        EndpointFilter.Status([
                            ComplianceDeclarationStatus.Submitted,
                            ComplianceDeclarationStatus.Accepted,
                        ])
                    )
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private async Task<string> RequestShouldBeBadRequest(EndpointQuery query)
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.ComplianceDeclarations.Search(query),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }
}
