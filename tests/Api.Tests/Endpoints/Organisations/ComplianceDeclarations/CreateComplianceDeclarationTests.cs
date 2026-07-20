using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

public class CreateComplianceDeclarationTests : EndpointTestBase
{
    public CreateComplianceDeclarationTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
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
    public async Task WhenOrganisationFound_ShouldBeCreated()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);
        ComplianceDeclarationService.CreateNewId = () => ObjectId.Parse("6830b9d4c7e21f5a8d3e64b2");
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

    [Fact]
    public async Task WhenReadOnlyUser_ShouldBeForbidden()
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.PostAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Create(Guid.NewGuid()),
            CreateComplianceDeclarationRequestFixture.Default().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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

    [Fact]
    public async Task Validation_WhenSubmitterLocaleMissing_ShouldBeBadRequest()
    {
        var content = await RequestShouldBeBadRequest(
            CreateComplianceDeclarationRequestFixture.Default().With(x => x.SubmitterLocale, (string?)null).Create()
        );

        await VerifyJson(content);
    }

    [Theory]
    [InlineData(SubmitterLocale.En)]
    [InlineData(SubmitterLocale.Cy)]
    public async Task WhenSubmitterLocaleProvided_ShouldPersistOnSubmittedAudit(string submitterLocale)
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);
        ComplianceDeclarationService.CreateNewId = () => ObjectId.Parse("6830b9d4c7e21f5a8d3e64b2");
        ComplianceDeclarationService.UtcNow = () => new DateTime(2026, 4, 20, 12, 28, 0, DateTimeKind.Utc);

        var response = await client.PostAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Create(FakeWasteOrganisationsService.OrganisationId),
            CreateComplianceDeclarationRequestFixture
                .DirectProducer(FakeWasteOrganisationsService.OrganisationId)
                .With(x => x.SubmitterLocale, submitterLocale)
                .Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );
        var submittedAudit = document
            .RootElement.GetProperty("audit")
            .EnumerateArray()
            .Should()
            .ContainSingle()
            .Subject;
        submittedAudit.GetProperty("action").GetString().Should().Be("Submitted");
        submittedAudit.GetProperty("submitterLocale").GetString().Should().Be(submitterLocale);
    }

    [Fact]
    public async Task Validation_WhenRequestInvalid_ShouldBeBadRequest()
    {
        var content = await RequestShouldBeBadRequest(
            new CreateComplianceDeclarationRequest
            {
                Organisation = null!,
                ObligationYear = 0,
                ObligationStatus = null!,
                SubmitterName = null!,
                User = null!,
                SubmitterLocale = null,
            }
        );

        await VerifyJson(content);
    }

    [Fact]
    public async Task WhenException_ShouldBeInternalServerError()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);
        WasteOrganisationsService.Throws = true;

        var response = await client.PostAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Create(Guid.NewGuid()),
            CreateComplianceDeclarationRequestFixture.Default().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
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
