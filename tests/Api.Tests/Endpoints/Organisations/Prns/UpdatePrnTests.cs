using System.Net;
using System.Net.Http.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Defra.WasteObligations.Testing.Fakes;
using Defra.WasteObligations.Testing.Fixtures.Dtos;
using Microsoft.Extensions.DependencyInjection;

namespace Defra.WasteObligations.Api.Tests.Endpoints.Organisations.Prns;

public class UpdatePrnTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
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
    public async Task WhenAccepted_ShouldUpdatePrnStatus()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);
        var prnId = Guid.NewGuid();

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.Prns.Update(
                FakeWasteOrganisationsService.OrganisationId,
                prnId.ToString("D")
            ),
            UpdatePrnRequestFixture.Accepted().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PrnCommonBackendService
            .LastPrnStatusUpdate.Should()
            .BeEquivalentTo(new PrnStatusUpdate { PrnId = prnId, Status = "ACCEPTED" });
    }

    [Fact]
    public async Task WhenRejected_ShouldUpdatePrnStatus()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);
        var prnId = Guid.NewGuid();

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.Prns.Update(
                FakeWasteOrganisationsService.OrganisationId,
                prnId.ToString("D")
            ),
            UpdatePrnRequestFixture.Rejected().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PrnCommonBackendService
            .LastPrnStatusUpdate.Should()
            .BeEquivalentTo(new PrnStatusUpdate { PrnId = prnId, Status = "REJECTED" });
    }

    [Fact]
    public async Task WhenRequestBodyMissing_ShouldBeBadRequestWithoutProxying()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);

        var response = await client.PatchAsync(
            Testing.Endpoints.Organisations.Prns.Update(
                FakeWasteOrganisationsService.OrganisationId,
                Guid.NewGuid().ToString("D")
            ),
            null,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        PrnCommonBackendService.LastPrnStatusUpdate.Should().BeNull();
    }

    [Fact]
    public async Task WhenStatusMissing_ShouldBeBadRequestWithoutProxying()
    {
        var response = await RequestShouldBeBadRequest(new { User = UserFixture.Regulator().Create() });

        PrnCommonBackendService.LastPrnStatusUpdate.Should().BeNull();
        await VerifyJson(response);
    }

    [Fact]
    public async Task WhenStatusInvalid_ShouldBeBadRequestWithoutProxying()
    {
        var response = await RequestShouldBeBadRequest(
            new { Status = "Cancelled", User = UserFixture.Regulator().Create() }
        );

        PrnCommonBackendService.LastPrnStatusUpdate.Should().BeNull();
        await VerifyJson(response);
    }

    [Fact]
    public async Task WhenStatusIsNotSupported_ShouldBeInternalServerErrorWithoutProxying()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.Prns.Update(
                FakeWasteOrganisationsService.OrganisationId,
                Guid.NewGuid().ToString("D")
            ),
            new { Status = 99, User = UserFixture.Regulator().Create() },
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        PrnCommonBackendService.LastPrnStatusUpdate.Should().BeNull();
    }

    [Fact]
    public async Task WhenUserIdInvalid_ShouldBeBadRequestWithoutProxying()
    {
        var response = await RequestShouldBeBadRequest(
            UpdatePrnRequestFixture
                .Accepted()
                .With(x => x.User, UserFixture.Regulator().With(x => x.Id, "not-a-guid").Create())
                .Create()
        );

        PrnCommonBackendService.LastPrnStatusUpdate.Should().BeNull();
        await VerifyJson(response);
    }

    [Fact]
    public async Task WhenOrganisationNotFound_ShouldBeNotFoundWithoutProxying()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.Prns.Update(Guid.NewGuid(), Guid.NewGuid().ToString("D")),
            UpdatePrnRequestFixture.Accepted().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        PrnCommonBackendService.LastPrnStatusUpdate.Should().BeNull();
    }

    [Fact]
    public async Task WhenPrnDoesNotBelongToOrganisation_ShouldBeNotFound()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);
        PrnCommonBackendService.StatusUpdateResult = PrnStatusUpdateResult.NotFound;

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.Prns.Update(
                FakeWasteOrganisationsService.OrganisationId,
                Guid.NewGuid().ToString("D")
            ),
            UpdatePrnRequestFixture.Accepted().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        PrnCommonBackendService.LastPrnStatusUpdate.Should().NotBeNull();
    }

    [Fact]
    public async Task WhenPrnNotFound_ShouldBeNotFound()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);
        PrnCommonBackendService.StatusUpdateResult = PrnStatusUpdateResult.NotFound;

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.Prns.Update(
                FakeWasteOrganisationsService.OrganisationId,
                Guid.NewGuid().ToString("D")
            ),
            UpdatePrnRequestFixture.Accepted().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WhenPrnCommonBackendReturnsUnsupportedResult_ShouldBeInternalServerError()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);
        PrnCommonBackendService.StatusUpdateResult = (PrnStatusUpdateResult)99;

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.Prns.Update(
                FakeWasteOrganisationsService.OrganisationId,
                Guid.NewGuid().ToString("D")
            ),
            UpdatePrnRequestFixture.Accepted().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        PrnCommonBackendService.LastPrnStatusUpdate.Should().NotBeNull();
    }

    [Fact]
    public async Task WhenPrnAlreadyHasRequestedStatus_ShouldBeOk()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.Prns.Update(
                FakeWasteOrganisationsService.OrganisationId,
                Guid.NewGuid().ToString("D")
            ),
            UpdatePrnRequestFixture.Accepted().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PrnCommonBackendService.LastPrnStatusUpdate.Should().NotBeNull();
    }

    [Fact]
    public async Task WhenPrnHasConflictingStatus_ShouldBeConflict()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);
        PrnCommonBackendService.ConcurrencyError = true;

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.Prns.Update(
                FakeWasteOrganisationsService.OrganisationId,
                Guid.NewGuid().ToString("D")
            ),
            UpdatePrnRequestFixture.Rejected().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task WhenPrnCommonBackendFails_ShouldBeInternalServerError()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);
        PrnCommonBackendService.Throws = true;

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.Prns.Update(
                FakeWasteOrganisationsService.OrganisationId,
                Guid.NewGuid().ToString("D")
            ),
            UpdatePrnRequestFixture.Accepted().Create(),
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
            Testing.Endpoints.Organisations.Prns.Update(
                FakeWasteOrganisationsService.OrganisationId,
                Guid.NewGuid().ToString("D")
            ),
            UpdatePrnRequestFixture.Accepted().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<string> RequestShouldBeBadRequest(object request)
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.Prns.Update(
                FakeWasteOrganisationsService.OrganisationId,
                Guid.NewGuid().ToString("D")
            ),
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }
}
