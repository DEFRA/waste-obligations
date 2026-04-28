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

namespace Defra.WasteObligations.Api.Tests.Endpoints.Organisations.ComplianceDeclarations;

public class CreateComplianceDeclarationTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    private FakeComplianceDeclarationService ComplianceDeclarationService { get; } = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddTransient<IWasteOrganisationsService>(_ => new FakeWasteOrganisationsService());
        services.AddTransient<IComplianceDeclarationService>(_ => ComplianceDeclarationService);
    }

    [Fact]
    public async Task WhenOrganisationFound_ShouldBeCreated()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);
        ComplianceDeclarationService.CreateNewId = () => new Guid("2d7e780c-ca82-4007-8b14-7c7ac49cf2f4");
        ComplianceDeclarationService.UtcNow = () => new DateTime(2026, 4, 20, 12, 28, 0, DateTimeKind.Utc);

        var response = await client.PostAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Create(FakeWasteOrganisationsService.OrganisationId),
            CreateComplianceDeclarationRequestFixture
                .DirectProducer(FakeWasteOrganisationsService.OrganisationId)
                .Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .DontScrubGuids()
            .DontScrubDateTimes();
    }

    [Fact]
    public async Task WhenNotFound_ShouldBeNotFound()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);

        var response = await client.PostAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Create(Guid.NewGuid()),
            CreateComplianceDeclarationRequestFixture.Default().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(2022)]
    [InlineData(2051)]
    public async Task Validation_WhenObligationYearInvalid_ShouldBeBadRequest(int obligationYear)
    {
        var content = await RequestShouldBeBadRequest(
            CreateComplianceDeclarationRequestFixture.Default().With(x => x.ObligationYear, obligationYear).Create()
        );

        await VerifyJson(content).UseParameters(obligationYear);
    }

    [Fact]
    public async Task Validation_WhenObligationMaterialInvalid_ShouldBeBadRequest()
    {
        var content = await RequestShouldBeBadRequest(
            CreateComplianceDeclarationRequestFixture
                .Default()
                .With(x => x.Obligations, [ObligationFixture.Default().With(x => x.Material, (string?)null).Create()])
                .Create()
        );

        await VerifyJson(content);
    }

    [Fact]
    public async Task Validation_WhenObligationStatusInvalid_ShouldBeBadRequest()
    {
        var content = await RequestShouldBeBadRequest(
            CreateComplianceDeclarationRequestFixture
                .Default()
                .With(x => x.Obligations, [ObligationFixture.Default().With(x => x.Status, (string?)null).Create()])
                .Create()
        );

        await VerifyJson(content);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public async Task Validation_WhenObligationRecyclingTargetInvalid_ShouldBeBadRequest(decimal recyclingTarget)
    {
        var content = await RequestShouldBeBadRequest(
            CreateComplianceDeclarationRequestFixture
                .Default()
                .With(
                    x => x.Obligations,
                    [ObligationFixture.Default().With(x => x.RecyclingTarget, recyclingTarget).Create()]
                )
                .Create()
        );

        await VerifyJson(content).UseParameters(recyclingTarget);
    }

    private async Task<string> RequestShouldBeBadRequest(CreateComplianceDeclarationRequest request)
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);

        var response = await client.PostAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Create(FakeWasteOrganisationsService.OrganisationId),
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }
}
