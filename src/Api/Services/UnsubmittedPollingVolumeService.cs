using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using OrganisationEligibilityMappers = Defra.WasteObligations.Api.Services.OrganisationEligibility.Mappers;

namespace Defra.WasteObligations.Api.Services;

public class UnsubmittedPollingVolumeService(
    IWasteOrganisationsService wasteOrganisationsService,
    ICurrentObligationYearProvider currentObligationYearProvider
) : IUnsubmittedPollingVolumeService
{
    private const string PlanningGeneration = "polling-volume";

    public async Task<UnsubmittedPollingVolume> Get(CancellationToken cancellationToken)
    {
        var source = await wasteOrganisationsService.Search(cancellationToken);
        var currentObligationYear = currentObligationYearProvider.GetCurrentObligationYear();
        var registeredRows = OrganisationEligibilityMappers
            .ToEligibilityRows(source.Organisations, PlanningGeneration, DateTimeOffset.UnixEpoch)
            .Where(x => x.RegistrationStatus == OrganisationRegistrationStatus.Registered);

        return new UnsubmittedPollingVolume
        {
            CurrentObligationYear = currentObligationYear,
            SourceOrganisationCount = source.Organisations.Length,
            Years =
            [
                .. registeredRows
                    .GroupBy(x => x.ObligationYear)
                    .OrderBy(x => x.Key)
                    .Select(group => new UnsubmittedPollingVolumeYear
                    {
                        ObligationYear = group.Key,
                        IsCurrentObligationYear = group.Key == currentObligationYear,
                        RegisteredOrganisationCount = group.Select(x => x.OrganisationId).Distinct().Count(),
                        RegisteredRegistrationCount = group.Count(),
                    }),
            ],
        };
    }
}
