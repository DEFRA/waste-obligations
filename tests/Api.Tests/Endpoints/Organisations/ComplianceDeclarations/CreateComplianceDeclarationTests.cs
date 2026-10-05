using System.Globalization;
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
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(content);
        document
            .RootElement.GetProperty("organisation")
            .GetProperty("businessCountry")
            .GetString()
            .Should()
            .Be("GB-ENG");

        await VerifyJson(content).DontScrubGuids().DontScrubDateTimes();
    }

    [Fact]
    public async Task WhenOrganisationDoesNotMatchRoute_ShouldBeBadRequestWithoutCreatingDeclaration()
    {
        var createCalled = false;
        ComplianceDeclarationService.CreateNewId = () =>
        {
            createCalled = true;

            return ObjectId.GenerateNewId();
        };

        var content = await RequestShouldBeBadRequest(
            CreateComplianceDeclarationRequestFixture
                .DirectProducer(Guid.Parse("e7df5c09-7ed6-4c8a-bf45-c324fc03e17e"))
                .Create()
        );

        createCalled.Should().BeFalse();
        await VerifyJson(content);
    }

    [Theory]
    [InlineData("2026-04-26T14:00:00Z", 2027)] // the year after the current obligation year
    [InlineData("2026-04-26T14:00:00Z", 2050)]
    [InlineData("2027-01-31T23:30:00Z", 2027)] // January still belongs to the 2026 obligation year
    [InlineData("2027-02-01T00:00:00Z", 2028)]
    public async Task WhenObligationYearIsAfterCurrentYear_ShouldBeBadRequestWithoutCreatingDeclaration(
        string utcNow,
        int obligationYear
    )
    {
        TimeProvider.SetUtcNow(DateTimeOffset.Parse(utcNow, CultureInfo.InvariantCulture));
        var createCalled = false;
        ComplianceDeclarationService.CreateNewId = () =>
        {
            createCalled = true;

            return ObjectId.GenerateNewId();
        };

        await RequestShouldBeBadRequest(
            CreateComplianceDeclarationRequestFixture
                .DirectProducer(FakeWasteOrganisationsService.OrganisationId)
                .With(x => x.ObligationYear, obligationYear)
                .Create()
        );

        createCalled.Should().BeFalse();
    }

    [Theory]
    [InlineData("2026-04-26T14:00:00Z", 2026)]
    [InlineData("2026-04-26T14:00:00Z", 2025)]
    [InlineData("2027-01-31T23:30:00Z", 2026)] // still the 2026 obligation year in January
    [InlineData("2027-02-01T00:00:00Z", 2027)]
    public async Task WhenObligationYearIsNotAfterCurrentYear_ShouldBeCreated(string utcNow, int obligationYear)
    {
        TimeProvider.SetUtcNow(DateTimeOffset.Parse(utcNow, CultureInfo.InvariantCulture));
        ComplianceDeclarationService.CreateNewId = () => ObjectId.GenerateNewId();
        var client = CreateClient(testUser: TestUser.WriteOnly);

        var response = await client.PostAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Create(FakeWasteOrganisationsService.OrganisationId),
            CreateComplianceDeclarationRequestFixture
                .DirectProducer(FakeWasteOrganisationsService.OrganisationId)
                .With(x => x.ObligationYear, obligationYear)
                .Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
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
    public async Task Validation_WhenUserLocaleMissing_ShouldBeBadRequest()
    {
        var content = await RequestShouldBeBadRequest(
            CreateComplianceDeclarationRequestFixture
                .Default()
                .With(x => x.User, UserFixture.WithoutLocale().Create())
                .Create()
        );

        await VerifyJson(content);
    }

    [Theory]
    [InlineData("EN")]
    [InlineData("fr")]
    public async Task Validation_WhenUserLocaleInvalid_ShouldBeBadRequest(string locale)
    {
        var content = await RequestShouldBeBadRequest(
            CreateComplianceDeclarationRequestFixture
                .Default()
                .With(x => x.User, UserFixture.Default().With(u => u.Locale, locale).Create())
                .Create()
        );

        await VerifyJson(content).UseParameters(locale);
    }

    [Theory]
    [InlineData(UserLocale.En)]
    [InlineData(UserLocale.Cy)]
    public async Task WhenUserLocaleProvided_ShouldPersistOnSubmittedAuditUser(string locale)
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);
        ComplianceDeclarationService.CreateNewId = () => ObjectId.Parse("6830b9d4c7e21f5a8d3e64b2");
        ComplianceDeclarationService.UtcNow = () => new DateTime(2026, 4, 20, 12, 28, 0, DateTimeKind.Utc);

        var response = await client.PostAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Create(FakeWasteOrganisationsService.OrganisationId),
            CreateComplianceDeclarationRequestFixture
                .DirectProducer(FakeWasteOrganisationsService.OrganisationId)
                .With(x => x.User, UserFixture.Default().With(u => u.Locale, locale).Create())
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
        submittedAudit.GetProperty("user").GetProperty("locale").GetString().Should().Be(locale);
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

    private async Task<string> RequestShouldBeBadRequest(object request)
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
