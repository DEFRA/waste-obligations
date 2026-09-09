using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Defra.WasteObligations.Testing.Fixtures.PrnCommonBackend;
using Defra.WasteObligations.Testing.Fixtures.WasteOrganisations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using PrnObligation = Defra.WasteObligations.Api.Services.PrnCommonBackend.Obligation;
using RegistrationType = Defra.WasteObligations.Api.Data.Entities.RegistrationType;
using WasteOrganisationsRegistrationType = Defra.WasteObligations.Api.Services.WasteOrganisations.RegistrationType;

namespace Defra.WasteObligations.Api.IntegrationTests.Services;

public class UnsubmittedOrganisationDetailsServiceTests : IntegrationTestBase
{
    private IPrnCommonBackendService PrnCommonBackendService { get; } = Substitute.For<IPrnCommonBackendService>();
    private IWasteOrganisationsService WasteOrganisationsService { get; } =
        Substitute.For<IWasteOrganisationsService>();

    [Fact]
    public async Task Get_ShouldReturnAllEligibilityGenerationsAndObligationSummariesForOrganisation()
    {
        const string activeGeneration = "active";
        var organisationId = Guid.Parse("3a67e998-ebd2-4dcc-9982-a12ef6db10a5");
        var utcNow = new DateTime(2026, 9, 6, 9, 0, 0, DateTimeKind.Utc);
        await OrganisationEligibilitySnapshots.InsertOneAsync(
            new OrganisationEligibilitySnapshot
            {
                Id = OrganisationEligibilitySnapshot.SnapshotId,
                ActiveGeneration = activeGeneration,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        await OrganisationComplianceDeclarationEligibilities.InsertManyAsync(
            [
                Eligibility(activeGeneration, organisationId, RegistrationType.ComplianceScheme, utcNow, 2026),
                Eligibility(activeGeneration, organisationId, RegistrationType.DirectProducer, utcNow, 2026),
                Eligibility("retained", organisationId, RegistrationType.DirectProducer, utcNow.AddDays(-1), 2025),
                Eligibility(activeGeneration, Guid.NewGuid(), RegistrationType.DirectProducer, utcNow, 2026),
            ],
            cancellationToken: TestContext.Current.CancellationToken
        );
        await OrganisationObligationSummaries.InsertManyAsync(
            [
                Summary(organisationId, 2026, utcNow),
                Summary(organisationId, 2025, utcNow.AddDays(-1)),
                Summary(Guid.NewGuid(), 2026, utcNow),
            ],
            cancellationToken: TestContext.Current.CancellationToken
        );
        var subject = CreateSubject();

        var result = await subject.Get(organisationId, false, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result.OrganisationId.Should().Be(organisationId);
        result.ActiveEligibilityGeneration.Should().Be(activeGeneration);
        result
            .Eligibility.Should()
            .BeEquivalentTo(
                [
                    new
                    {
                        Generation = activeGeneration,
                        ObligationYear = 2026,
                        RegistrationType = Defra.WasteObligations.Api.Dtos.RegistrationType.ComplianceScheme,
                        RegistrationStatus = "Registered",
                        Country = "GB-ENG",
                        Name = "Example organisation",
                        TradingName = "Example trading name",
                        CompaniesHouseNumber = "01234567",
                        ReferenceNumber = "100001",
                        ReferenceResolutionState = "Resolved",
                        IsVisibleInUnsubmittedView = true,
                        RecyclingObligationsMet = (bool?)false,
                        ObligationCoveragePercentage = (decimal?)34.5m,
                        DeclarationStateUpdatedAt = utcNow.AddMinutes(-1),
                        SourceFingerprint = "eligibility-fingerprint",
                        RefreshedAt = utcNow,
                    },
                    new
                    {
                        Generation = activeGeneration,
                        ObligationYear = 2026,
                        RegistrationType = Defra.WasteObligations.Api.Dtos.RegistrationType.DirectProducer,
                        RegistrationStatus = "Registered",
                        Country = "GB-ENG",
                        Name = "Example organisation",
                        TradingName = "Example trading name",
                        CompaniesHouseNumber = "01234567",
                        ReferenceNumber = "100001",
                        ReferenceResolutionState = "Resolved",
                        IsVisibleInUnsubmittedView = true,
                        RecyclingObligationsMet = (bool?)false,
                        ObligationCoveragePercentage = (decimal?)34.5m,
                        DeclarationStateUpdatedAt = utcNow.AddMinutes(-1),
                        SourceFingerprint = "eligibility-fingerprint",
                        RefreshedAt = utcNow,
                    },
                    new
                    {
                        Generation = "retained",
                        ObligationYear = 2025,
                        RegistrationType = Defra.WasteObligations.Api.Dtos.RegistrationType.DirectProducer,
                        RegistrationStatus = "Registered",
                        Country = "GB-ENG",
                        Name = "Example organisation",
                        TradingName = "Example trading name",
                        CompaniesHouseNumber = "01234567",
                        ReferenceNumber = "100001",
                        ReferenceResolutionState = "Resolved",
                        IsVisibleInUnsubmittedView = true,
                        RecyclingObligationsMet = (bool?)false,
                        ObligationCoveragePercentage = (decimal?)34.5m,
                        DeclarationStateUpdatedAt = utcNow.AddDays(-1).AddMinutes(-1),
                        SourceFingerprint = "eligibility-fingerprint",
                        RefreshedAt = utcNow.AddDays(-1),
                    },
                ],
                options => options.WithStrictOrdering()
            );
        result
            .ObligationSummaries.Should()
            .BeEquivalentTo(
                [
                    new
                    {
                        ObligationYear = 2026,
                        ObligationCount = 7,
                        TotalAcceptedTonnage = 123,
                        TotalObligatedTonnage = 456,
                        RecyclingObligationsMet = (bool?)false,
                        ObligationCoveragePercentage = (decimal?)34.5m,
                        SourceFingerprint = "summary-fingerprint",
                        LastSuccessfulReadAt = (DateTime?)utcNow.AddMinutes(-30),
                        DailyCalculationRunId = "run-id",
                        LastAttemptedAt = utcNow,
                        NextRefreshAt = utcNow.AddMinutes(30),
                        Priority = "ScheduledRefresh",
                        RequestedAt = utcNow.AddHours(-1),
                        IsHydrationActive = true,
                        RefreshState = "Ready",
                        AttemptCount = 3,
                        LastFailure = (string?)null,
                    },
                    new
                    {
                        ObligationYear = 2025,
                        ObligationCount = 7,
                        TotalAcceptedTonnage = 123,
                        TotalObligatedTonnage = 456,
                        RecyclingObligationsMet = (bool?)false,
                        ObligationCoveragePercentage = (decimal?)34.5m,
                        SourceFingerprint = "summary-fingerprint",
                        LastSuccessfulReadAt = (DateTime?)utcNow.AddDays(-1).AddMinutes(-30),
                        DailyCalculationRunId = "run-id",
                        LastAttemptedAt = utcNow.AddDays(-1),
                        NextRefreshAt = utcNow.AddDays(-1).AddMinutes(30),
                        Priority = "ScheduledRefresh",
                        RequestedAt = utcNow.AddDays(-1).AddHours(-1),
                        IsHydrationActive = true,
                        RefreshState = "Ready",
                        AttemptCount = 3,
                        LastFailure = (string?)null,
                    },
                ],
                options => options.WithStrictOrdering()
            );
        result.LiveData.Should().BeNull();
        await WasteOrganisationsService.DidNotReceive().Read(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await PrnCommonBackendService
            .DidNotReceive()
            .ReadObligations(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_WhenOrganisationHasNoUnsubmittedData_ShouldReturnNull()
    {
        var subject = CreateSubject();

        var result = await subject.Get(Guid.NewGuid(), false, TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Get_WhenLiveDataIsRequested_ShouldReturnRawObligationsAndUnsubmittedCalculations()
    {
        var organisationId = Guid.Parse("3a67e998-ebd2-4dcc-9982-a12ef6db10a5");
        var organisation = OrganisationFixture
            .Default(organisationId)
            .With(
                x => x.Registrations,
                [
                    RegistrationFixture
                        .Default()
                        .With(x => x.RegistrationYear, 2025)
                        .With(x => x.Type, WasteOrganisationsRegistrationType.LargeProducer)
                        .Create(),
                    RegistrationFixture
                        .Default()
                        .With(x => x.RegistrationYear, 2026)
                        .With(x => x.Type, WasteOrganisationsRegistrationType.ComplianceScheme)
                        .With(x => x.Status, "Cancelled")
                        .Create(),
                    RegistrationFixture
                        .Default()
                        .With(x => x.RegistrationYear, 2026)
                        .With(x => x.Type, WasteOrganisationsRegistrationType.LargeProducer)
                        .Create(),
                ]
            )
            .Create();
        var obligations2025 = new[]
        {
            ObligationFixture
                .Default()
                .With(x => x.OrganisationId, organisationId)
                .With(x => x.MaterialName, "Glass")
                .With(x => x.MaterialTarget, 0.76m)
                .With(x => x.Tonnage, 20)
                .With(x => x.ObligationToMeet, (int?)10)
                .With(x => x.TonnageAccepted, 12)
                .With(x => x.TonnageOutstanding, (int?)0)
                .With(x => x.Status, "Met")
                .Create(),
        };
        var obligations2026 = new[]
        {
            ObligationFixture
                .Default()
                .With(x => x.OrganisationId, organisationId)
                .With(x => x.MaterialName, "Plastic")
                .With(x => x.MaterialTarget, 0.57m)
                .With(x => x.Tonnage, 20)
                .With(x => x.ObligationToMeet, (int?)20)
                .With(x => x.TonnageAccepted, 15)
                .With(x => x.TonnageOutstanding, (int?)5)
                .With(x => x.Status, "NotMet")
                .Create(),
        };
        WasteOrganisationsService.Read(organisationId, Arg.Any<CancellationToken>()).Returns(organisation);
        PrnCommonBackendService
            .ReadObligations(organisationId, 2025, Arg.Any<CancellationToken>())
            .Returns(obligations2025);
        PrnCommonBackendService
            .ReadObligations(organisationId, 2026, Arg.Any<CancellationToken>())
            .Returns(obligations2026);
        var subject = CreateSubject();

        var result = await subject.Get(organisationId, true, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result.Eligibility.Should().BeEmpty();
        result.ObligationSummaries.Should().BeEmpty();
        result.LiveData.Should().NotBeNull();
        result
            .LiveData.Organisation.Should()
            .BeEquivalentTo(
                new
                {
                    Id = organisationId,
                    Name = "Test Name Ltd",
                    TradingName = "Trading Name",
                    Country = "GB-ENG",
                    CompaniesHouseNumber = "12345678",
                    Address = new
                    {
                        AddressLine1 = organisation.Address.AddressLine1,
                        AddressLine2 = organisation.Address.AddressLine2,
                        Town = organisation.Address.Town,
                        County = organisation.Address.County,
                        Postcode = organisation.Address.Postcode,
                        Country = organisation.Address.Country,
                    },
                    Registrations = new[]
                    {
                        new
                        {
                            Status = "REGISTERED",
                            Type = WasteOrganisationsRegistrationType.LargeProducer,
                            RegistrationYear = 2025,
                        },
                        new
                        {
                            Status = "Cancelled",
                            Type = WasteOrganisationsRegistrationType.ComplianceScheme,
                            RegistrationYear = 2026,
                        },
                        new
                        {
                            Status = "REGISTERED",
                            Type = WasteOrganisationsRegistrationType.LargeProducer,
                            RegistrationYear = 2026,
                        },
                    },
                }
            );
        result
            .LiveData.ObligationResults.Should()
            .BeEquivalentTo(
                [
                    new
                    {
                        ObligationYear = 2025,
                        RawObligations = new[]
                        {
                            new
                            {
                                OrganisationId = organisationId,
                                MaterialName = "Glass",
                                Tonnage = 20,
                                MaterialTarget = 0.76m,
                                ObligationToMeet = (int?)10,
                                TonnageAwaitingAcceptance = 10,
                                TonnageAccepted = 12,
                                TonnageOutstanding = (int?)0,
                                Status = "Met",
                            },
                        },
                        UnsubmittedCalculation = new
                        {
                            ObligationCount = 1,
                            TotalAcceptedTonnage = 12,
                            TotalObligatedTonnage = 10,
                            RecyclingObligationsMet = (bool?)true,
                            ObligationCoveragePercentage = 100m,
                        },
                    },
                    new
                    {
                        ObligationYear = 2026,
                        RawObligations = new[]
                        {
                            new
                            {
                                OrganisationId = organisationId,
                                MaterialName = "Plastic",
                                Tonnage = 20,
                                MaterialTarget = 0.57m,
                                ObligationToMeet = (int?)20,
                                TonnageAwaitingAcceptance = 10,
                                TonnageAccepted = 15,
                                TonnageOutstanding = (int?)5,
                                Status = "NotMet",
                            },
                        },
                        UnsubmittedCalculation = new
                        {
                            ObligationCount = 1,
                            TotalAcceptedTonnage = 15,
                            TotalObligatedTonnage = 20,
                            RecyclingObligationsMet = (bool?)false,
                            ObligationCoveragePercentage = 75m,
                        },
                    },
                ],
                options => options.WithStrictOrdering()
            );
        await WasteOrganisationsService.Received(1).Read(organisationId, Arg.Any<CancellationToken>());
        await PrnCommonBackendService.Received(1).ReadObligations(organisationId, 2025, Arg.Any<CancellationToken>());
        await PrnCommonBackendService.Received(1).ReadObligations(organisationId, 2026, Arg.Any<CancellationToken>());
    }

    private UnsubmittedOrganisationDetailsService CreateSubject() =>
        new(
            new MongoDbContext(
                GetMongoApplicationDatabase(),
                Options.Create(new MongoDbOptions()),
                NullLogger<MongoDbContext>.Instance
            ),
            WasteOrganisationsService,
            PrnCommonBackendService
        );

    private static OrganisationComplianceDeclarationEligibility Eligibility(
        string generation,
        Guid organisationId,
        RegistrationType registrationType,
        DateTime refreshedAt,
        int obligationYear
    ) =>
        new()
        {
            Generation = generation,
            OrganisationId = organisationId,
            ObligationYear = obligationYear,
            RegistrationType = registrationType,
            RegistrationStatus = OrganisationRegistrationStatus.Registered,
            BusinessCountry = "GB-ENG",
            Name = "Example organisation",
            TradingName = "Example trading name",
            CompaniesHouseNumber = "01234567",
            ReferenceNumber = "100001",
            ReferenceNumberResolutionState = OrganisationReferenceNumberResolutionState.Resolved,
            IsVisibleInUnsubmittedView = true,
            RecyclingObligationsMet = false,
            ObligationCoveragePercentage = 34.5m,
            DeclarationStateUpdatedAt = refreshedAt.AddMinutes(-1),
            SourceFingerprint = "eligibility-fingerprint",
            RefreshedAt = refreshedAt,
        };

    private static OrganisationObligationSummary Summary(
        Guid organisationId,
        int obligationYear,
        DateTime attemptedAt
    ) =>
        new()
        {
            OrganisationId = organisationId,
            ObligationYear = obligationYear,
            ObligationCount = 7,
            TotalAcceptedTonnage = 123,
            TotalObligatedTonnage = 456,
            RecyclingObligationsMet = false,
            ObligationCoveragePercentage = 34.5m,
            SourceFingerprint = "summary-fingerprint",
            LastSuccessfulReadAt = attemptedAt.AddMinutes(-30),
            DailyCalculationRunId = "run-id",
            LastAttemptedAt = attemptedAt,
            NextRefreshAt = attemptedAt.AddMinutes(30),
            Priority = OrganisationObligationHydrationPriority.ScheduledRefresh,
            RequestedAt = attemptedAt.AddHours(-1),
            IsHydrationActive = true,
            RefreshState = OrganisationObligationRefreshState.Ready,
            AttemptCount = 3,
        };
}
