using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.OrganisationObligations;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Defra.WasteObligations.Testing.Fixtures.WasteOrganisations;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Organisation = Defra.WasteObligations.Api.Services.WasteOrganisations.Organisation;
using Registration = Defra.WasteObligations.Api.Services.WasteOrganisations.Registration;
using SourceRegistrationType = Defra.WasteObligations.Api.Services.WasteOrganisations.RegistrationType;

namespace Defra.WasteObligations.Api.Tests.Services;

public class UnsubmittedHistoricalBackfillServiceTests
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 9, 8, 16, 0, 0, TimeSpan.Zero));
    private ICurrentObligationYearProvider CurrentObligationYearProvider { get; } =
        Substitute.For<ICurrentObligationYearProvider>();
    private IOrganisationObligationHistoricalBackfillStore HistoricalBackfillStore { get; } =
        Substitute.For<IOrganisationObligationHistoricalBackfillStore>();
    private IWasteOrganisationsService WasteOrganisationsService { get; } =
        Substitute.For<IWasteOrganisationsService>();

    [Fact]
    public async Task Start_ShouldCreateOneBackfillPerHistoricalYearFromRegisteredSupportedOrganisations()
    {
        var firstOrganisationId = Guid.NewGuid();
        var secondOrganisationId = Guid.NewGuid();
        WasteOrganisationsService
            .Search(Arg.Any<CancellationToken>())
            .Returns(
                new OrganisationSearch
                {
                    Organisations =
                    [
                        CreateOrganisation(
                            firstOrganisationId,
                            [
                                CreateRegistration(
                                    SourceRegistrationType.LargeProducer,
                                    RegistrationStatus.Registered,
                                    2024
                                ),
                                CreateRegistration(
                                    SourceRegistrationType.ComplianceScheme,
                                    RegistrationStatus.Registered,
                                    2025
                                ),
                                CreateRegistration(
                                    SourceRegistrationType.LargeProducer,
                                    RegistrationStatus.Registered,
                                    2026
                                ),
                            ]
                        ),
                        CreateOrganisation(
                            secondOrganisationId,
                            [
                                CreateRegistration(
                                    SourceRegistrationType.LargeProducer,
                                    RegistrationStatus.Registered,
                                    2025
                                ),
                                CreateRegistration(
                                    SourceRegistrationType.LargeProducer,
                                    RegistrationStatus.Cancelled,
                                    2024
                                ),
                                CreateRegistration(
                                    SourceRegistrationType.LargeProducer,
                                    RegistrationStatus.Registered,
                                    2027
                                ),
                            ]
                        ),
                    ],
                }
            );
        CurrentObligationYearProvider.GetCurrentObligationYear().Returns(2026);
        HistoricalBackfillStore
            .Create(Arg.Any<OrganisationObligationHistoricalBackfill>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var backfill = callInfo.Arg<OrganisationObligationHistoricalBackfill>();

                return new OrganisationObligationHistoricalBackfillCreation { Backfill = backfill, WasCreated = true };
            });
        var subject = CreateSubject();

        var result = await subject.Start(TestContext.Current.CancellationToken);

        result
            .Should()
            .BeEquivalentTo(
                new
                {
                    CurrentObligationYear = 2026,
                    Years = new[]
                    {
                        new
                        {
                            ObligationYear = 2024,
                            PotentialHydrationOrganisationCount = 1,
                            Status = "Started",
                        },
                        new
                        {
                            ObligationYear = 2025,
                            PotentialHydrationOrganisationCount = 2,
                            Status = "Started",
                        },
                    },
                }
            );
        var expected2025OrganisationIds = new[] { firstOrganisationId, secondOrganisationId }.Order();
        await HistoricalBackfillStore
            .Received(1)
            .Create(
                Arg.Is<OrganisationObligationHistoricalBackfill>(x =>
                    x.ObligationYear == 2024
                    && x.OrganisationIds.SequenceEqual(new[] { firstOrganisationId })
                    && x.RequestedAt == _timeProvider.GetUtcNow().UtcDateTime
                ),
                TestContext.Current.CancellationToken
            );
        await HistoricalBackfillStore
            .Received(1)
            .Create(
                Arg.Is<OrganisationObligationHistoricalBackfill>(x =>
                    x.ObligationYear == 2025
                    && x.OrganisationIds.Order().SequenceEqual(expected2025OrganisationIds)
                    && x.RequestedAt == _timeProvider.GetUtcNow().UtcDateTime
                ),
                TestContext.Current.CancellationToken
            );
    }

    [Fact]
    public async Task Start_WhenHistoricalBackfillIsAlreadyComplete_ShouldNotRestartIt()
    {
        var organisationId = Guid.NewGuid();
        WasteOrganisationsService
            .Search(Arg.Any<CancellationToken>())
            .Returns(
                new OrganisationSearch
                {
                    Organisations =
                    [
                        CreateOrganisation(
                            organisationId,
                            [
                                CreateRegistration(
                                    SourceRegistrationType.LargeProducer,
                                    RegistrationStatus.Registered,
                                    2025
                                ),
                            ]
                        ),
                    ],
                }
            );
        CurrentObligationYearProvider.GetCurrentObligationYear().Returns(2026);
        HistoricalBackfillStore
            .Create(Arg.Any<OrganisationObligationHistoricalBackfill>(), Arg.Any<CancellationToken>())
            .Returns(
                new OrganisationObligationHistoricalBackfillCreation
                {
                    Backfill = new OrganisationObligationHistoricalBackfill
                    {
                        ObligationYear = 2025,
                        OrganisationIds = [organisationId],
                        RequestedAt = _timeProvider.GetUtcNow().AddDays(-1).UtcDateTime,
                        UpdatedAt = _timeProvider.GetUtcNow().AddDays(-1).UtcDateTime,
                        CompletedAt = _timeProvider.GetUtcNow().AddHours(-1).UtcDateTime,
                    },
                    WasCreated = false,
                }
            );
        var subject = CreateSubject();

        var result = await subject.Start(TestContext.Current.CancellationToken);

        result
            .Years.Should()
            .BeEquivalentTo([
                new
                {
                    ObligationYear = 2025,
                    PotentialHydrationOrganisationCount = 1,
                    Status = "AlreadyCompleted",
                },
            ]);
    }

    private UnsubmittedHistoricalBackfillService CreateSubject() =>
        new(WasteOrganisationsService, CurrentObligationYearProvider, HistoricalBackfillStore, _timeProvider);

    private static Organisation CreateOrganisation(Guid organisationId, Registration[] registrations) =>
        OrganisationFixture.Default(organisationId).With(x => x.Registrations, registrations).Create();

    private static Registration CreateRegistration(string type, string status, int registrationYear) =>
        RegistrationFixture
            .Default()
            .With(x => x.Type, type)
            .With(x => x.Status, status)
            .With(x => x.RegistrationYear, registrationYear)
            .Create();
}
