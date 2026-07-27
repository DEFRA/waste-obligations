using System.Net;
using System.Net.Http.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Testing;
using Defra.WasteObligations.Testing.Authentication;
using Defra.WasteObligations.Testing.Extensions.WireMock;
using Defra.WasteObligations.Testing.Fixtures.PrnCommonBackend;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.IntegrationTests.Scenarios;

public class SearchPrnsTests : IntegrationTestBase
{
    [Fact]
    public async Task WhenNoPrnsMatch_ShouldReturnEmptyPagedPrns()
    {
        await WireMockContext.WireMockAdminApi.StubTokenRequest(
            expiryInSeconds: 60,
            clientId: ClientIds.PrnCommonBackend
        );
        var organisationId = Guid.NewGuid();
        await WireMockContext.WireMockAdminApi.StubWasteOrganisationsOrganisationRequest(
            organisationId,
            BasicAuthCredential.ForClient(ClientIds.WasteOrganisations)
        );
        const string searchTerm = "no matching PRNs";
        var search = new PrnSearchRequest
        {
            Page = 1,
            PageSize = 20,
            Search = searchTerm,
            SortBy = "date-issued-desc",
        };
        await WireMockContext.WireMockAdminApi.StubPrnCommonBackendPrnSearchRequest(
            search,
            new PrnSearchResponse(),
            organisationId.ToString("D"),
            OAuth2Extensions.AccessToken
        );

        var client = CreateClient();

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Prns.Search(
                organisationId,
                EndpointQuery.New.Where(EndpointFilter.Search(searchTerm))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PrnsPaged>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result.Prns.Should().BeEmpty();
        result.Total.Should().Be(0);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task WhenOrganisationAndPrnsFound_ShouldReturnPagedPrns()
    {
        await WireMockContext.WireMockAdminApi.StubTokenRequest(
            expiryInSeconds: 60,
            clientId: ClientIds.PrnCommonBackend
        );
        var organisationId = Guid.NewGuid();
        await WireMockContext.WireMockAdminApi.StubWasteOrganisationsOrganisationRequest(
            organisationId,
            BasicAuthCredential.ForClient(ClientIds.WasteOrganisations)
        );
        var prn = PrnDataFixture.Default().With(x => x.OrganisationId, organisationId).Create();
        const string searchTerm = "PRN123";
        var search = new PrnSearchRequest
        {
            Page = 2,
            PageSize = 50,
            Search = searchTerm,
            FilterBy = "accepted-all",
            SortBy = "tonnage-asc",
        };
        await WireMockContext.WireMockAdminApi.StubPrnCommonBackendPrnSearchRequest(
            search,
            new PrnSearchResponse { Items = [prn], TotalItems = 51 },
            organisationId.ToString("D"),
            OAuth2Extensions.AccessToken
        );

        var client = CreateClient();

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Prns.Search(
                organisationId,
                EndpointQuery
                    .New.Where(EndpointFilter.Status("Accepted"))
                    .Where(EndpointFilter.Search(searchTerm))
                    .Where(EndpointFilter.Sort("TonnageAscending"))
                    .Where(EndpointFilter.Page(search.Page))
                    .Where(EndpointFilter.PageSize(search.PageSize))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PrnsPaged>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result.Total.Should().Be(51);
        result.Page.Should().Be(search.Page);
        result.PageSize.Should().Be(search.PageSize);
        result.Prns.Should().ContainSingle().Which.Id.Should().Be(prn.ExternalId.ToString("D"));
    }

    [Fact]
    public async Task WhenPrnCommonBackendReturnsServerError_ShouldReturnInternalServerError()
    {
        await WireMockContext.WireMockAdminApi.StubTokenRequest(
            expiryInSeconds: 60,
            clientId: ClientIds.PrnCommonBackend
        );
        var organisationId = Guid.NewGuid();
        await WireMockContext.WireMockAdminApi.StubWasteOrganisationsOrganisationRequest(
            organisationId,
            BasicAuthCredential.ForClient(ClientIds.WasteOrganisations)
        );
        var search = new PrnSearchRequest
        {
            Page = 1,
            PageSize = 20,
            SortBy = "date-issued-desc",
        };
        await WireMockContext.WireMockAdminApi.StubPrnCommonBackendPrnSearchRequest(
            search,
            new PrnSearchResponse(),
            organisationId.ToString("D"),
            OAuth2Extensions.AccessToken,
            HttpStatusCode.ServiceUnavailable
        );

        var client = CreateClient();

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Prns.Search(organisationId),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var result = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result.Status.Should().Be((int)HttpStatusCode.InternalServerError);
        result.Title.Should().Be("An error occurred while processing your request.");
        result.Detail.Should().BeNull();
    }
}
