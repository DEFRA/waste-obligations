using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services.OrganisationObligations;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Services;

public class UnsubmittedPollingPlanService(
    IUnsubmittedPollingVolumeService pollingVolumeService,
    ICurrentObligationYearProvider currentObligationYearProvider,
    IOptions<OrganisationObligationHydrationOptions> hydrationOptions,
    IOrganisationObligationHistoricalBackfillStore historicalBackfillStore,
    ILogger<UnsubmittedPollingPlanService> logger
) : IUnsubmittedPollingPlanService
{
    private const string PotentialCountWarning =
        "Potential hydration organisation counts exclude Account reference resolution.";
    private const string SourceReadWarning =
        "Waste Organisations data could not be read, so no polling plan could be calculated.";

    public async Task<UnsubmittedPollingPlan> Get(CancellationToken cancellationToken)
    {
        var options = hydrationOptions.Value;
        UnsubmittedPollingVolume volume;

        try
        {
            volume = await pollingVolumeService.Get(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to read Waste Organisations data for unsubmitted polling plan");

            return new UnsubmittedPollingPlan
            {
                CurrentObligationYear = currentObligationYearProvider.GetCurrentObligationYear(),
                TargetFullRefreshMinutes = options.RefreshInterval.TotalMinutes,
                SafetyCeilingRequestsPerMinute = options.MaxDownstreamRequestsPerMinute,
                RecommendedRateHeadroomPercentage = options.RecommendedRateHeadroomPercentage,
                SourceReadSucceeded = false,
                SourceOrganisationCount = null,
                Warnings = [SourceReadWarning],
                Years = [],
            };
        }

        var historicalBackfills = await historicalBackfillStore.GetAll(cancellationToken);

        return Plan(volume, options, historicalBackfills);
    }

    private static UnsubmittedPollingPlan Plan(
        UnsubmittedPollingVolume volume,
        OrganisationObligationHydrationOptions options,
        IReadOnlyList<OrganisationObligationHistoricalBackfill> historicalBackfills
    )
    {
        var volumesByYear = volume.Years.ToDictionary(x => x.ObligationYear);
        var years = volumesByYear.Keys.Append(volume.CurrentObligationYear).Order().Distinct();
        var completedBackfillYears = historicalBackfills
            .Where(x => x.CompletedAt is not null)
            .Select(x => x.ObligationYear)
            .ToHashSet();

        return new UnsubmittedPollingPlan
        {
            CurrentObligationYear = volume.CurrentObligationYear,
            TargetFullRefreshMinutes = options.RefreshInterval.TotalMinutes,
            SafetyCeilingRequestsPerMinute = options.MaxDownstreamRequestsPerMinute,
            RecommendedRateHeadroomPercentage = options.RecommendedRateHeadroomPercentage,
            SourceReadSucceeded = true,
            SourceOrganisationCount = volume.SourceOrganisationCount,
            Warnings = [PotentialCountWarning],
            Years =
            [
                .. years.Select(year =>
                {
                    var volumeForYear = volumesByYear.GetValueOrDefault(year);
                    var organisationCount = volumeForYear?.RegisteredOrganisationCount ?? 0;
                    var requiredRequestsPerMinute = RequiredRequestsPerMinute(
                        organisationCount,
                        options.RefreshInterval
                    );

                    return new UnsubmittedPollingPlanYear
                    {
                        ObligationYear = year,
                        Classification = Classification(year, volume.CurrentObligationYear, completedBackfillYears),
                        PotentialHydrationOrganisationCount = organisationCount,
                        RegisteredRegistrationCount = volumeForYear?.RegisteredRegistrationCount ?? 0,
                        RequiredRequestsPerMinute = requiredRequestsPerMinute,
                        RecommendedRequestsPerMinute = RecommendedRequestsPerMinute(
                            requiredRequestsPerMinute,
                            options.RecommendedRateHeadroomPercentage
                        ),
                        EstimatedFullRefreshMinutesAtSafetyCeiling =
                            organisationCount / (double)options.MaxDownstreamRequestsPerMinute,
                    };
                }),
            ],
        };
    }

    private static string Classification(
        int obligationYear,
        int currentObligationYear,
        HashSet<int> completedBackfillYears
    ) =>
        obligationYear switch
        {
            _ when completedBackfillYears.Contains(obligationYear) => "Excluded",
            _ when obligationYear < currentObligationYear => "HistoricalBackfill",
            _ when obligationYear == currentObligationYear => "Current",
            _ => "Excluded",
        };

    private static int RequiredRequestsPerMinute(int organisationCount, TimeSpan targetFullRefresh) =>
        (int)Math.Ceiling(organisationCount / targetFullRefresh.TotalMinutes);

    private static int RecommendedRequestsPerMinute(int requiredRequestsPerMinute, int headroomPercentage) =>
        (int)Math.Ceiling(requiredRequestsPerMinute * (1 + headroomPercentage / 100d));
}
