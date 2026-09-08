using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.OrganisationObligations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Services;

public class UnsubmittedPollingPlanServiceTests
{
    private IUnsubmittedPollingVolumeService PollingVolumeService { get; } =
        Substitute.For<IUnsubmittedPollingVolumeService>();
    private IOrganisationObligationHistoricalBackfillStore HistoricalBackfillStore { get; } =
        Substitute.For<IOrganisationObligationHistoricalBackfillStore>();
    private ICurrentObligationYearProvider CurrentObligationYearProvider { get; } =
        Substitute.For<ICurrentObligationYearProvider>();

    [Fact]
    public async Task Get_ShouldCalculatePlanForCurrentHistoricalAndFutureYears()
    {
        PollingVolumeService
            .Get(Arg.Any<CancellationToken>())
            .Returns(
                new UnsubmittedPollingVolume
                {
                    CurrentObligationYear = 2026,
                    SourceOrganisationCount = 15,
                    Years =
                    [
                        new UnsubmittedPollingVolumeYear
                        {
                            ObligationYear = 2025,
                            IsCurrentObligationYear = false,
                            RegisteredOrganisationCount = 31,
                            RegisteredRegistrationCount = 35,
                        },
                        new UnsubmittedPollingVolumeYear
                        {
                            ObligationYear = 2027,
                            IsCurrentObligationYear = false,
                            RegisteredOrganisationCount = 9,
                            RegisteredRegistrationCount = 10,
                        },
                    ],
                }
            );
        HistoricalBackfillStore.GetAll(Arg.Any<CancellationToken>()).Returns([]);
        var subject = CreateSubject(
            new OrganisationObligationHydrationOptions
            {
                RefreshInterval = TimeSpan.FromMinutes(30),
                MaxDownstreamRequestsPerMinute = 2,
                RecommendedRateHeadroomPercentage = 20,
            }
        );

        var result = await subject.Get(TestContext.Current.CancellationToken);

        result
            .Should()
            .BeEquivalentTo(
                new
                {
                    CurrentObligationYear = 2026,
                    TargetFullRefreshMinutes = 30d,
                    SafetyCeilingRequestsPerMinute = 2,
                    RecommendedRateHeadroomPercentage = 20,
                    SourceReadSucceeded = true,
                    SourceOrganisationCount = 15,
                    Warnings = new[]
                    {
                        "Potential hydration organisation counts exclude Account reference resolution.",
                    },
                    Years = new[]
                    {
                        new
                        {
                            ObligationYear = 2025,
                            Classification = "HistoricalBackfill",
                            PotentialHydrationOrganisationCount = 31,
                            RegisteredRegistrationCount = 35,
                            RequiredRequestsPerMinute = 2,
                            RecommendedRequestsPerMinute = 3,
                            EstimatedFullRefreshMinutesAtSafetyCeiling = 15.5d,
                        },
                        new
                        {
                            ObligationYear = 2026,
                            Classification = "Current",
                            PotentialHydrationOrganisationCount = 0,
                            RegisteredRegistrationCount = 0,
                            RequiredRequestsPerMinute = 0,
                            RecommendedRequestsPerMinute = 0,
                            EstimatedFullRefreshMinutesAtSafetyCeiling = 0d,
                        },
                        new
                        {
                            ObligationYear = 2027,
                            Classification = "Excluded",
                            PotentialHydrationOrganisationCount = 9,
                            RegisteredRegistrationCount = 10,
                            RequiredRequestsPerMinute = 1,
                            RecommendedRequestsPerMinute = 2,
                            EstimatedFullRefreshMinutesAtSafetyCeiling = 4.5d,
                        },
                    },
                }
            );
    }

    [Fact]
    public async Task Get_WhenWasteOrganisationsCannotBeRead_ShouldReturnDiagnosticPlan()
    {
        PollingVolumeService
            .Get(Arg.Any<CancellationToken>())
            .Returns(
                Task.FromException<UnsubmittedPollingVolume>(
                    new HttpRequestException("Waste Organisations unavailable")
                )
            );
        var subject = CreateSubject(new OrganisationObligationHydrationOptions());

        var result = await subject.Get(TestContext.Current.CancellationToken);

        result
            .Should()
            .BeEquivalentTo(
                new
                {
                    CurrentObligationYear = 2026,
                    TargetFullRefreshMinutes = 30d,
                    SafetyCeilingRequestsPerMinute = 20,
                    RecommendedRateHeadroomPercentage = 20,
                    SourceReadSucceeded = false,
                    SourceOrganisationCount = (int?)null,
                    Warnings = new[]
                    {
                        "Waste Organisations data could not be read, so no polling plan could be calculated.",
                    },
                    Years = Array.Empty<UnsubmittedPollingPlanYear>(),
                }
            );
    }

    [Fact]
    public async Task Get_WhenHistoricalBackfillIsComplete_ShouldExcludeItsYearFromAProposedPlan()
    {
        PollingVolumeService
            .Get(Arg.Any<CancellationToken>())
            .Returns(
                new UnsubmittedPollingVolume
                {
                    CurrentObligationYear = 2026,
                    SourceOrganisationCount = 1,
                    Years =
                    [
                        new UnsubmittedPollingVolumeYear
                        {
                            ObligationYear = 2025,
                            IsCurrentObligationYear = false,
                            RegisteredOrganisationCount = 1,
                            RegisteredRegistrationCount = 1,
                        },
                    ],
                }
            );
        HistoricalBackfillStore
            .GetAll(Arg.Any<CancellationToken>())
            .Returns([
                new OrganisationObligationHistoricalBackfill
                {
                    ObligationYear = 2025,
                    OrganisationIds = [Guid.NewGuid()],
                    RequestedAt = DateTime.UnixEpoch,
                    UpdatedAt = DateTime.UnixEpoch,
                    CompletedAt = DateTime.UnixEpoch,
                },
            ]);
        var subject = CreateSubject(new OrganisationObligationHydrationOptions());

        var result = await subject.Get(TestContext.Current.CancellationToken);

        result.Years.Single(x => x.ObligationYear == 2025).Classification.Should().Be("Excluded");
    }

    private UnsubmittedPollingPlanService CreateSubject(OrganisationObligationHydrationOptions options) =>
        CreateSubjectWithCurrentYear(options);

    private UnsubmittedPollingPlanService CreateSubjectWithCurrentYear(OrganisationObligationHydrationOptions options)
    {
        CurrentObligationYearProvider.GetCurrentObligationYear().Returns(2026);

        return new UnsubmittedPollingPlanService(
            PollingVolumeService,
            CurrentObligationYearProvider,
            Options.Create(options),
            HistoricalBackfillStore,
            Substitute.For<ILogger<UnsubmittedPollingPlanService>>()
        );
    }
}
