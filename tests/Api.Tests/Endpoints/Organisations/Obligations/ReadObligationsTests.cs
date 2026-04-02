using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Testing;

namespace Defra.WasteObligations.Api.Tests.Endpoints.Organisations.Obligations;

public class ReadObligationsTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    [Theory]
    [InlineData(IncludeTypes.Organisation)]
    [InlineData(null)]
    public async Task WhenFound_ShouldReturnObligations(string? include)
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetStringAsync(
            Testing.Endpoints.Organisations.Obligations.Read(
                FakeOrganisationService.OrganisationId,
                EndpointQuery.New.Where(EndpointFilter.Year(2026)).Where(EndpointFilter.Include(include))
            ),
            TestContext.Current.CancellationToken
        );

        await VerifyJson(response).DontScrubGuids().UseParameters(include);
    }

    [Fact]
    public async Task WhenNotFound_ShouldBeNotFound()
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Obligations.Read(
                Guid.NewGuid(),
                EndpointQuery.New.Where(EndpointFilter.Year(2026))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WhenInvalidYear_ShouldBeBadRequest()
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Obligations.Read(
                Guid.NewGuid(),
                EndpointQuery.New.Where(EndpointFilter.Year(1999))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
