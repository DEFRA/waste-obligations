using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Defra.WasteObligations.Testing.Fakes;
using Defra.WasteObligations.Testing.Fixtures.Dtos;
using Defra.WasteObligations.Testing.Fixtures.PrnCommonBackend;
using Microsoft.Extensions.DependencyInjection;

namespace Defra.WasteObligations.Api.Tests.Endpoints.Organisations.Prns;

public class UpdatePrnsTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    private FakeWasteOrganisationsService WasteOrganisationsService { get; } = new();
    private FakePrnCommonBackendService PrnCommonBackendService { get; } = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddTransient<IWasteOrganisationsService>(_ => WasteOrganisationsService);
        services.AddTransient<IPrnCommonBackendService>(_ => PrnCommonBackendService);
    }

    [Fact]
    public async Task WhenAccepted_ShouldUpdateEveryPrnStatus()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);
        List<string> prnIds = [Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D")];

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.Prns.UpdateMany(FakeWasteOrganisationsService.OrganisationId),
            UpdatePrnsRequestFixture.Accepted().With(x => x.PrnIds, prnIds).Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PrnCommonBackendService
            .LastPrnStatusUpdates.Should()
            .BeEquivalentTo(
                prnIds.Select(x => new PrnStatusUpdate
                {
                    PrnId = Guid.Parse(x),
                    Status = PrnCommonBackendStatus.Accepted,
                })
            );
    }

    [Fact]
    public async Task WhenRejected_ShouldUpdateEveryPrnStatus()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);
        List<string> prnIds = [Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D")];

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.Prns.UpdateMany(FakeWasteOrganisationsService.OrganisationId),
            UpdatePrnsRequestFixture.Rejected().With(x => x.PrnIds, prnIds).Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PrnCommonBackendService
            .LastPrnStatusUpdates.Should()
            .BeEquivalentTo(
                prnIds.Select(x => new PrnStatusUpdate
                {
                    PrnId = Guid.Parse(x),
                    Status = PrnCommonBackendStatus.Rejected,
                })
            );
    }

    [Fact]
    public async Task WhenPrnIdsRepeated_ShouldUpdateEachPrnOnce()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);
        var prnId = Guid.NewGuid();

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.Prns.UpdateMany(FakeWasteOrganisationsService.OrganisationId),
            UpdatePrnsRequestFixture
                .Accepted()
                .With(x => x.PrnIds, [prnId.ToString("D"), prnId.ToString("D").ToUpperInvariant()])
                .Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PrnCommonBackendService
            .LastPrnStatusUpdates.Should()
            .BeEquivalentTo([new PrnStatusUpdate { PrnId = prnId, Status = PrnCommonBackendStatus.Accepted }]);
    }

    [Fact]
    public async Task WhenRequestBodyMissing_ShouldBeBadRequestWithoutProxying()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);

        var response = await client.PatchAsync(
            Testing.Endpoints.Organisations.Prns.UpdateMany(FakeWasteOrganisationsService.OrganisationId),
            null,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        PrnCommonBackendService.LastPrnStatusUpdates.Should().BeNull();
    }

    [Fact]
    public async Task WhenPrnIdsMissing_ShouldBeBadRequestWithoutProxying()
    {
        var response = await RequestShouldBeBadRequest(
            new { Status = UpdatePrnStatus.Accepted, User = UserFixture.Regulator().Create() }
        );

        PrnCommonBackendService.LastPrnStatusUpdates.Should().BeNull();
        await VerifyJson(response);
    }

    [Fact]
    public async Task WhenPrnIdsEmpty_ShouldBeBadRequestWithoutProxying()
    {
        var response = await RequestShouldBeBadRequest(
            UpdatePrnsRequestFixture.Accepted().With(x => x.PrnIds, []).Create()
        );

        PrnCommonBackendService.LastPrnStatusUpdates.Should().BeNull();
        await VerifyJson(response);
    }

    [Fact]
    public async Task WhenTooManyPrnIds_ShouldBeBadRequestWithoutProxying()
    {
        var response = await RequestShouldBeBadRequest(
            UpdatePrnsRequestFixture
                .Accepted()
                .With(
                    x => x.PrnIds,
                    [.. Enumerable.Range(0, UpdatePrnsRequest.MaxPrnIds + 1).Select(_ => Guid.NewGuid().ToString("D"))]
                )
                .Create()
        );

        PrnCommonBackendService.LastPrnStatusUpdates.Should().BeNull();
        await VerifyJson(response);
    }

    [Fact]
    public async Task WhenPrnIdInvalid_ShouldBeBadRequestWithoutProxying()
    {
        var response = await RequestShouldBeBadRequest(
            UpdatePrnsRequestFixture
                .Accepted()
                .With(x => x.PrnIds, [Guid.NewGuid().ToString("D"), "not-a-guid"])
                .Create()
        );

        PrnCommonBackendService.LastPrnStatusUpdates.Should().BeNull();
        await VerifyJson(response);
    }

    // The year is the caller's choice and is not checked against the PRNs'
    // own obligation years, so any year in range is sent as given.
    [Theory]
    [InlineData(2026)]
    [InlineData(2027)]
    [InlineData(2030)]
    public async Task WhenObligationYearGiven_ShouldSendItAsGivenForEveryPrn(int obligationYear)
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);
        var prnIds = AddAwaitingPrns(obligationYear: "2026", count: 2);

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.Prns.UpdateMany(FakeWasteOrganisationsService.OrganisationId),
            UpdatePrnsRequestFixture
                .Accepted()
                .With(x => x.PrnIds, prnIds)
                .With(x => x.ObligationYear, obligationYear)
                .Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PrnCommonBackendService
            .LastPrnStatusUpdates.Should()
            .HaveCount(2)
            .And.OnlyContain(x => x.ObligationYear == obligationYear.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task WhenObligationYearGivenAndAPrnIsNoLongerAwaitingAcceptance_ShouldBeConflictWithoutUpdating()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);
        var prnIds = AddAwaitingPrns(obligationYear: "2026", count: 2);
        PrnCommonBackendService.Prns[Guid.Parse(prnIds[1])] = PrnCommonBackendService.Prns[Guid.Parse(prnIds[1])] with
        {
            PrnStatus = PrnCommonBackendStatus.Accepted,
        };

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.Prns.UpdateMany(FakeWasteOrganisationsService.OrganisationId),
            UpdatePrnsRequestFixture.Accepted().With(x => x.PrnIds, prnIds).With(x => x.ObligationYear, 2026).Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        PrnCommonBackendService.LastPrnStatusUpdates.Should().BeNull();
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WhenObligationYearGivenAndAPrnIsNotFound_ShouldBeNotFoundWithoutUpdating()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);
        List<string> prnIds = [.. AddAwaitingPrns(obligationYear: "2026", count: 1), Guid.NewGuid().ToString("D")];

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.Prns.UpdateMany(FakeWasteOrganisationsService.OrganisationId),
            UpdatePrnsRequestFixture.Accepted().With(x => x.PrnIds, prnIds).With(x => x.ObligationYear, 2026).Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        PrnCommonBackendService.LastPrnStatusUpdates.Should().BeNull();
    }

    [Fact]
    public async Task WhenObligationYearGivenWithRejected_ShouldBeBadRequestWithoutProxying()
    {
        var response = await RequestShouldBeBadRequest(
            UpdatePrnsRequestFixture.Rejected().With(x => x.ObligationYear, 2026).Create()
        );

        PrnCommonBackendService.LastPrnStatusUpdates.Should().BeNull();
        await VerifyJson(response);
    }

    [Fact]
    public async Task WhenObligationYearOmitted_ShouldNotSendOne()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.Prns.UpdateMany(FakeWasteOrganisationsService.OrganisationId),
            UpdatePrnsRequestFixture.Accepted().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PrnCommonBackendService.LastPrnStatusUpdates.Should().OnlyContain(x => x.ObligationYear == null);
    }

    [Fact]
    public async Task WhenObligationYearOutOfRange_ShouldBeBadRequestWithoutProxying()
    {
        var response = await RequestShouldBeBadRequest(
            UpdatePrnsRequestFixture.Accepted().With(x => x.ObligationYear, 1999).Create()
        );

        PrnCommonBackendService.LastPrnStatusUpdates.Should().BeNull();
        await VerifyJson(response);
    }

    [Fact]
    public async Task WhenStatusMissing_ShouldBeBadRequestWithoutProxying()
    {
        var response = await RequestShouldBeBadRequest(
            new { User = UserFixture.Regulator().Create(), PrnIds = new[] { Guid.NewGuid().ToString("D") } }
        );

        PrnCommonBackendService.LastPrnStatusUpdates.Should().BeNull();
        await VerifyJson(response);
    }

    [Fact]
    public async Task WhenOrganisationNotFound_ShouldBeNotFoundWithoutProxying()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.Prns.UpdateMany(Guid.NewGuid()),
            UpdatePrnsRequestFixture.Accepted().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        PrnCommonBackendService.LastPrnStatusUpdates.Should().BeNull();
    }

    [Fact]
    public async Task WhenAnyPrnNotFound_ShouldBeNotFound()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);
        PrnCommonBackendService.StatusUpdateResult = PrnStatusUpdateResult.NotFound;

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.Prns.UpdateMany(FakeWasteOrganisationsService.OrganisationId),
            UpdatePrnsRequestFixture.Accepted().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WhenAnyPrnHasConflictingStatus_ShouldBeConflict()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);
        PrnCommonBackendService.ConcurrencyError = true;

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.Prns.UpdateMany(FakeWasteOrganisationsService.OrganisationId),
            UpdatePrnsRequestFixture.Accepted().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WhenPrnCommonBackendReturnsUnsupportedResult_ShouldBeInternalServerError()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);
        PrnCommonBackendService.StatusUpdateResult = (PrnStatusUpdateResult)99;

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.Prns.UpdateMany(FakeWasteOrganisationsService.OrganisationId),
            UpdatePrnsRequestFixture.Accepted().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        PrnCommonBackendService.LastPrnStatusUpdates.Should().NotBeNull();
    }

    [Fact]
    public async Task WhenPrnCommonBackendFails_ShouldBeInternalServerError()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);
        PrnCommonBackendService.Throws = true;

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.Prns.UpdateMany(FakeWasteOrganisationsService.OrganisationId),
            UpdatePrnsRequestFixture.Accepted().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WhenReadOnlyUser_ShouldBeForbidden()
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.Prns.UpdateMany(FakeWasteOrganisationsService.OrganisationId),
            UpdatePrnsRequestFixture.Accepted().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        PrnCommonBackendService.LastPrnStatusUpdates.Should().BeNull();
    }

    private async Task<string> RequestShouldBeBadRequest(object request)
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.Prns.UpdateMany(FakeWasteOrganisationsService.OrganisationId),
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private List<string> AddAwaitingPrns(string obligationYear, int count)
    {
        List<string> prnIds = [];

        for (var i = 0; i < count; i++)
        {
            var prn = PrnDataFixture
                .Default()
                .With(x => x.ExternalId, Guid.NewGuid())
                .With(x => x.OrganisationId, FakeWasteOrganisationsService.OrganisationId)
                .With(x => x.PrnStatus, PrnCommonBackendStatus.AwaitingAcceptance)
                .With(x => x.ObligationYear, obligationYear)
                .Create();
            PrnCommonBackendService.Prns[prn.ExternalId] = prn;
            prnIds.Add(prn.ExternalId.ToString("D"));
        }

        return prnIds;
    }
}
