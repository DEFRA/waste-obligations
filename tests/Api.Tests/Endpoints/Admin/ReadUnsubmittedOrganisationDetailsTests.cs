using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Endpoints.Admin;

public class ReadUnsubmittedOrganisationDetailsTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    private IUnsubmittedOrganisationDetailsService OrganisationDetailsService { get; } =
        Substitute.For<IUnsubmittedOrganisationDetailsService>();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddTransient<IUnsubmittedOrganisationDetailsService>(_ => OrganisationDetailsService);
    }

    [Fact]
    public async Task WhenAdmin_ShouldReturnOrganisationDetails()
    {
        var organisationId = Guid.Parse("3a67e998-ebd2-4dcc-9982-a12ef6db10a5");
        OrganisationDetailsService
            .Get(organisationId, false, Arg.Any<CancellationToken>())
            .Returns(OrganisationDetails(organisationId));
        var client = CreateClient(testUser: TestUser.Admin);

        var response = await client.GetAsync(
            Testing.Endpoints.Admin.UnsubmittedOrganisationDetails(organisationId),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        await OrganisationDetailsService.Received(1).Get(organisationId, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenAdminRequestsLiveData_ShouldReturnLiveDataAlongsideOrganisationDetails()
    {
        var organisationId = Guid.Parse("3a67e998-ebd2-4dcc-9982-a12ef6db10a5");
        OrganisationDetailsService
            .Get(organisationId, true, Arg.Any<CancellationToken>())
            .Returns(OrganisationDetails(organisationId) with { LiveData = LiveData(organisationId) });
        var client = CreateClient(testUser: TestUser.Admin);

        var response = await client.GetAsync(
            Testing.Endpoints.Admin.UnsubmittedOrganisationDetails(organisationId, includeLiveData: true),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        await OrganisationDetailsService.Received(1).Get(organisationId, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenOrganisationHasNoUnsubmittedData_ShouldBeNotFound()
    {
        var client = CreateClient(testUser: TestUser.Admin);

        var response = await client.GetAsync(
            Testing.Endpoints.Admin.UnsubmittedOrganisationDetails(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WhenReadWriteUser_ShouldBeForbidden()
    {
        var client = CreateClient();

        var response = await client.GetAsync(
            Testing.Endpoints.Admin.UnsubmittedOrganisationDetails(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task WhenUnauthenticated_ShouldBeUnauthorized()
    {
        var client = CreateClient(addAuthorizationHeader: false);

        var response = await client.GetAsync(
            Testing.Endpoints.Admin.UnsubmittedOrganisationDetails(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    private static UnsubmittedOrganisationDetails OrganisationDetails(Guid organisationId) =>
        new()
        {
            OrganisationId = organisationId,
            ActiveEligibilityGeneration = "active-generation",
            Eligibility =
            [
                new UnsubmittedOrganisationEligibility
                {
                    Generation = "active-generation",
                    ObligationYear = 2026,
                    RegistrationType = RegistrationType.DirectProducer,
                    RegistrationStatus = "Registered",
                    Country = "GB-ENG",
                    Name = "Example organisation",
                    TradingName = "Example trading name",
                    CompaniesHouseNumber = "01234567",
                    ReferenceNumber = "100001",
                    ReferenceResolutionState = "Resolved",
                    IsVisibleInUnsubmittedView = true,
                    RecyclingObligationsMet = false,
                    ObligationCoveragePercentage = 34.5m,
                    DeclarationStateUpdatedAt = new DateTime(2026, 9, 6, 9, 0, 0, DateTimeKind.Utc),
                    SourceFingerprint = "eligibility-fingerprint",
                    RefreshedAt = new DateTime(2026, 9, 6, 9, 1, 0, DateTimeKind.Utc),
                },
            ],
            ObligationSummaries =
            [
                new UnsubmittedOrganisationObligationSummary
                {
                    ObligationYear = 2026,
                    ObligationCount = 7,
                    TotalAcceptedTonnage = 123,
                    TotalObligatedTonnage = 456,
                    RecyclingObligationsMet = false,
                    ObligationCoveragePercentage = 34.5m,
                    SourceFingerprint = "summary-fingerprint",
                    LastSuccessfulReadAt = new DateTime(2026, 9, 6, 8, 30, 0, DateTimeKind.Utc),
                    DailyCalculationRunId = "run-id",
                    LastAttemptedAt = new DateTime(2026, 9, 6, 9, 2, 0, DateTimeKind.Utc),
                    NextRefreshAt = new DateTime(2026, 9, 6, 9, 32, 0, DateTimeKind.Utc),
                    Priority = "ScheduledRefresh",
                    RequestedAt = new DateTime(2026, 9, 6, 8, 30, 0, DateTimeKind.Utc),
                    IsHydrationActive = true,
                    RefreshState = "Ready",
                    AttemptCount = 3,
                    LastFailure = "Temporary downstream error",
                },
            ],
        };

    private static UnsubmittedOrganisationLiveData LiveData(Guid organisationId) =>
        new()
        {
            Organisation = new UnsubmittedOrganisationLiveOrganisation
            {
                Id = organisationId,
                Name = "Live organisation",
                TradingName = "Live trading name",
                Country = "GB-ENG",
                CompaniesHouseNumber = "01234567",
                Address = new Address
                {
                    AddressLine1 = "1 Test Street",
                    Town = "Test Town",
                    Postcode = "TE1 1ST",
                    Country = "England",
                },
                Registrations =
                [
                    new UnsubmittedOrganisationLiveRegistration
                    {
                        Status = "Registered",
                        Type = "LargeProducer",
                        RegistrationYear = 2026,
                        Created = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                        Updated = new DateTimeOffset(2026, 9, 6, 9, 0, 0, TimeSpan.Zero),
                    },
                ],
            },
            ObligationResults =
            [
                new UnsubmittedOrganisationLiveObligationResult
                {
                    ObligationYear = 2026,
                    RawObligations =
                    [
                        new UnsubmittedOrganisationRawObligation
                        {
                            OrganisationId = organisationId,
                            MaterialName = "Plastic",
                            Tonnage = 370,
                            MaterialTarget = 0.57m,
                            ObligationToMeet = 211,
                            TonnageAwaitingAcceptance = 0,
                            TonnageAccepted = 126,
                            TonnageOutstanding = 85,
                            Status = "NotMet",
                        },
                    ],
                    UnsubmittedCalculation = new UnsubmittedOrganisationLiveObligationCalculation
                    {
                        ObligationCount = 1,
                        TotalAcceptedTonnage = 126,
                        TotalObligatedTonnage = 211,
                        RecyclingObligationsMet = false,
                        ObligationCoveragePercentage = 60m,
                        SourceFingerprint = "live-fingerprint",
                    },
                },
            ],
        };
}
