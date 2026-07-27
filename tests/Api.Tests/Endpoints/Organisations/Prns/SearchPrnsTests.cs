using System.Net;
using System.Net.Http.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Defra.WasteObligations.Testing;
using Defra.WasteObligations.Testing.Fakes;
using Defra.WasteObligations.Testing.Fixtures.Dtos;
using Defra.WasteObligations.Testing.Fixtures.PrnCommonBackend;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Endpoints.Organisations.Prns;

public class SearchPrnsTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    private IPrnCommonBackendService PrnCommonBackendService { get; } = Substitute.For<IPrnCommonBackendService>();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddTransient<IWasteOrganisationsService>(_ => new FakeWasteOrganisationsService());
        services.AddTransient<IPrnCommonBackendService>(_ => PrnCommonBackendService);
    }

    [Fact]
    public async Task WhenValid_ShouldReturnPrnsPaged()
    {
        var prn = PrnDataFixture
            .Default()
            .With(x => x.OrganisationId, FakeWasteOrganisationsService.OrganisationId)
            .Create();
        StubSearchResponse([prn], totalItems: 21);
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Prns.Search(FakeWasteOrganisationsService.OrganisationId),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PrnsPaged>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result.Prns.Should().BeEquivalentTo([PrnFixture.Default().Create()]);
        result.Total.Should().Be(21);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        await PrnCommonBackendService
            .Received(1)
            .SearchPrns(
                FakeWasteOrganisationsService.OrganisationId,
                Arg.Is<PrnSearchRequest>(x =>
                    x.Page == 1
                    && x.PageSize == 20
                    && x.Search == null
                    && x.FilterBy == null
                    && x.SortBy == "date-issued-desc"
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Theory]
    [InlineData("AwaitingAcceptance", "awaiting-all")]
    [InlineData("Accepted", "accepted-all")]
    [InlineData("Rejected", "rejected-all")]
    [InlineData("Cancelled", "cancelled-all")]
    public async Task WhenStatusSpecified_ShouldMapStatusFilter(string status, string filterBy)
    {
        StubSearchResponse([]);
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Prns.Search(
                FakeWasteOrganisationsService.OrganisationId,
                EndpointQuery.New.Where(EndpointFilter.Status(status))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await PrnCommonBackendService
            .Received(1)
            .SearchPrns(
                FakeWasteOrganisationsService.OrganisationId,
                Arg.Is<PrnSearchRequest>(x => x.FilterBy == filterBy),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task WhenSearchSpecified_ShouldMapSearch()
    {
        const string search = "PRN123 Acme";
        StubSearchResponse([]);
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Prns.Search(
                FakeWasteOrganisationsService.OrganisationId,
                EndpointQuery.New.Where(EndpointFilter.Search(search))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await PrnCommonBackendService
            .Received(1)
            .SearchPrns(
                FakeWasteOrganisationsService.OrganisationId,
                Arg.Is<PrnSearchRequest>(x => x.Search == search),
                Arg.Any<CancellationToken>()
            );
    }

    [Theory]
    [InlineData("IssuedAtDescending", "date-issued-desc")]
    [InlineData("IssuedAtAscending", "date-issued-asc")]
    [InlineData("TonnageDescending", "tonnage-desc")]
    [InlineData("TonnageAscending", "tonnage-asc")]
    [InlineData("IssuerDescending", "issued-by-desc")]
    [InlineData("IssuerAscending", "issued-by-asc")]
    [InlineData("DecemberWasteDescending", "december-waste-desc")]
    [InlineData("MaterialDescending", "material-desc")]
    [InlineData("MaterialAscending", "material-asc")]
    public async Task WhenSortSpecified_ShouldMapSort(string sort, string sortBy)
    {
        StubSearchResponse([]);
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Prns.Search(
                FakeWasteOrganisationsService.OrganisationId,
                EndpointQuery.New.Where(EndpointFilter.Sort(sort))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await PrnCommonBackendService
            .Received(1)
            .SearchPrns(
                FakeWasteOrganisationsService.OrganisationId,
                Arg.Is<PrnSearchRequest>(x => x.SortBy == sortBy),
                Arg.Any<CancellationToken>()
            );
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("AwaitingCancellation")]
    [InlineData("0")]
    public async Task Validation_WhenStatusInvalid_ShouldBeBadRequest(string status)
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Prns.Search(
                FakeWasteOrganisationsService.OrganisationId,
                EndpointQuery.New.Where(EndpointFilter.Status(status))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await PrnCommonBackendService
            .DidNotReceive()
            .SearchPrns(Arg.Any<Guid>(), Arg.Any<PrnSearchRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("0")]
    public async Task Validation_WhenSortInvalid_ShouldBeBadRequest(string sort)
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Prns.Search(
                FakeWasteOrganisationsService.OrganisationId,
                EndpointQuery.New.Where(EndpointFilter.Sort(sort))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await PrnCommonBackendService
            .DidNotReceive()
            .SearchPrns(Arg.Any<Guid>(), Arg.Any<PrnSearchRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validation_WhenPageInvalid_ShouldBeBadRequest(int page)
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Prns.Search(
                FakeWasteOrganisationsService.OrganisationId,
                EndpointQuery.New.Where(EndpointFilter.Page(page))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Validation_WhenPageSizeInvalid_ShouldBeBadRequest(int pageSize)
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Prns.Search(
                FakeWasteOrganisationsService.OrganisationId,
                EndpointQuery.New.Where(EndpointFilter.PageSize(pageSize))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task WhenPageSpecified_ShouldReturnRequestedPagingValues()
    {
        StubSearchResponse([], totalItems: 100);
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Prns.Search(
                FakeWasteOrganisationsService.OrganisationId,
                EndpointQuery.New.Where(EndpointFilter.Page(5)).Where(EndpointFilter.PageSize(100))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PrnsPaged>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result.Page.Should().Be(5);
        result.PageSize.Should().Be(100);
        result.Total.Should().Be(100);
        await PrnCommonBackendService
            .Received(1)
            .SearchPrns(
                FakeWasteOrganisationsService.OrganisationId,
                Arg.Is<PrnSearchRequest>(x => x.Page == 5 && x.PageSize == 100),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task WhenOrganisationNotFound_ShouldBeNotFound()
    {
        StubSearchResponse([]);
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Prns.Search(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WhenSearchResultRecipientDoesNotMatchOrganisation_ShouldBeNotFound()
    {
        StubSearchResponse([PrnDataFixture.Default().Create()]);
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Prns.Search(FakeWasteOrganisationsService.OrganisationId),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WhenPrnCommonBackendIsUnreachable_ShouldBeInternalServerError()
    {
        PrnCommonBackendService
            .SearchPrns(Arg.Any<Guid>(), Arg.Any<PrnSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<PrnSearchResponse>(new HttpRequestException()));
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Prns.Search(FakeWasteOrganisationsService.OrganisationId),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task WhenWriteOnlyUser_ShouldBeForbidden()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Prns.Search(FakeWasteOrganisationsService.OrganisationId),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private void StubSearchResponse(IEnumerable<PrnData> items, int totalItems = 0)
    {
        PrnCommonBackendService
            .SearchPrns(Arg.Any<Guid>(), Arg.Any<PrnSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PrnSearchResponse { Items = items, TotalItems = totalItems }));
    }
}
