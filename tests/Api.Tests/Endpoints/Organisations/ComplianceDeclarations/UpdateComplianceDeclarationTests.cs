using System.Net;
using System.Net.Http.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Defra.WasteObligations.Testing.Fakes;
using Defra.WasteObligations.Testing.Fixtures.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;

namespace Defra.WasteObligations.Api.Tests.Endpoints.Organisations.ComplianceDeclarations;

public class UpdateComplianceDeclarationTests : EndpointTestBase
{
    public UpdateComplianceDeclarationTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
        : base(factory, outputHelper)
    {
        TimeProvider = new FakeTimeProvider();
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 4, 26, 14, 0, 0, TimeSpan.Zero));
    }

    private FakeTimeProvider TimeProvider { get; }
    private FakeComplianceDeclarationService ComplianceDeclarationService { get; } = new();
    private FakeWasteOrganisationsService WasteOrganisationsService { get; } = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddTransient<IWasteOrganisationsService>(_ => WasteOrganisationsService);
        services.AddTransient<IComplianceDeclarationService>(_ => ComplianceDeclarationService);
        services.AddTransient<TimeProvider>(_ => TimeProvider);
    }

    [Fact]
    public async Task WhenNotFound_ShouldBeNotFound()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Update(
                Guid.NewGuid(),
                ObjectId.GenerateNewId().ToString()
            ),
            UpdateComplianceDeclarationRequestFixture.Default().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WhenReadOnlyUser_ShouldBeForbidden()
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Update(
                Guid.NewGuid(),
                ObjectId.GenerateNewId().ToString()
            ),
            UpdateComplianceDeclarationRequestFixture.Default().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Validation_WhenCancellingAndReasonIsMissing_ShouldBeBadRequest()
    {
        var content = await RequestShouldBeBadRequest(
            UpdateComplianceDeclarationRequestFixture.Cancelled().With(x => x.Reason, (string?)null).Create()
        );

        await VerifyJson(content);
    }

    [Fact]
    public async Task Validation_WhenNoUser_ShouldBeBadRequest()
    {
        var content = await RequestShouldBeBadRequest(
            UpdateComplianceDeclarationRequestFixture.Default().With(x => x.User, (User?)null).Create()
        );

        await VerifyJson(content);
    }

    [Fact]
    public async Task Validation_WhenUnknownPayload_ShouldBeBadRequest()
    {
        var content = await RequestShouldBeBadRequest(new { Status = "Unknown" });

        await VerifyJson(content);
    }

    [Fact]
    public async Task WhenException_ShouldBeInternalServerError()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);
        WasteOrganisationsService.Throws = true;

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Update(
                Guid.NewGuid(),
                ObjectId.GenerateNewId().ToString()
            ),
            UpdateComplianceDeclarationRequestFixture.Default().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WhenStatusIsNotChanging_ShouldBeConflict()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Update(
                FakeWasteOrganisationsService.OrganisationId,
                FakeComplianceDeclarationService.ComplianceDeclarationId.ToString()
            ),
            UpdateComplianceDeclarationRequestFixture.Default().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WhenConcurrentUpdate_ShouldBeUnprocessableEntity()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);
        ComplianceDeclarationService.ConcurrencyError = true;

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Update(
                FakeWasteOrganisationsService.OrganisationId,
                FakeComplianceDeclarationService.ComplianceDeclarationId.ToString()
            ),
            UpdateComplianceDeclarationRequestFixture
                .Default()
                .With(x => x.Status, ComplianceDeclarationStatus.Accepted)
                .Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WhenUpdated_ShouldBeOk()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Update(
                FakeWasteOrganisationsService.OrganisationId,
                FakeComplianceDeclarationService.ComplianceDeclarationId.ToString()
            ),
            UpdateComplianceDeclarationRequestFixture.Accepted().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WhenOrganisationFound_ButOrganisationDoesNotMatch_ShouldBeNotFound()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Update(
                FakeWasteOrganisationsService.OrganisationId,
                FakeComplianceDeclarationService.NonMatchingOrganisationComplianceDeclarationId.ToString()
            ),
            UpdateComplianceDeclarationRequestFixture.Accepted().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<string> RequestShouldBeBadRequest(object request)
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Update(
                FakeWasteOrganisationsService.OrganisationId,
                FakeComplianceDeclarationService.ComplianceDeclarationId.ToString()
            ),
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }
}
