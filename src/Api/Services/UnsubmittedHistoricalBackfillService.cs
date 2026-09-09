using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Extensions;
using Defra.WasteObligations.Api.Services.OrganisationObligations;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using OrganisationEligibilityMappers = Defra.WasteObligations.Api.Services.OrganisationEligibility.Mappers;

namespace Defra.WasteObligations.Api.Services;

public class UnsubmittedHistoricalBackfillService(
    IWasteOrganisationsService wasteOrganisationsService,
    ICurrentObligationYearProvider currentObligationYearProvider,
    IOrganisationObligationHistoricalBackfillStore historicalBackfillStore,
    TimeProvider timeProvider
) : IUnsubmittedHistoricalBackfillService
{
    private const string BackfillGeneration = "historical-obligation-backfill";

    public async Task<UnsubmittedHistoricalBackfillStart> Start(CancellationToken cancellationToken)
    {
        var source = await wasteOrganisationsService.Search(cancellationToken);
        var currentObligationYear = currentObligationYearProvider.GetCurrentObligationYear();
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        var registeredRows = OrganisationEligibilityMappers
            .ToEligibilityRows(source.Organisations, BackfillGeneration, utcNow)
            .Where(x =>
                x.RegistrationStatus == OrganisationRegistrationStatus.Registered
                && x.ObligationYear < currentObligationYear
            );
        var years = await Task.WhenAll(
            registeredRows
                .GroupBy(x => x.ObligationYear)
                .OrderBy(x => x.Key)
                .Select(async group =>
                {
                    var creation = await historicalBackfillStore.Create(
                        new OrganisationObligationHistoricalBackfill
                        {
                            ObligationYear = group.Key,
                            OrganisationIds = group.Select(x => x.OrganisationId).Distinct().ToArray(),
                            RequestedAt = utcNow,
                            UpdatedAt = utcNow,
                        },
                        cancellationToken
                    );

                    return new UnsubmittedHistoricalBackfillYear
                    {
                        ObligationYear = group.Key,
                        PotentialHydrationOrganisationCount = creation.Backfill.OrganisationIds.Length,
                        Status = Status(creation),
                    };
                })
        );

        return new UnsubmittedHistoricalBackfillStart { CurrentObligationYear = currentObligationYear, Years = years };
    }

    private static string Status(OrganisationObligationHistoricalBackfillCreation creation) =>
        creation switch
        {
            { WasCreated: true } => "Started",
            { Backfill.CompletedAt: not null } => "AlreadyCompleted",
            _ => "AlreadyRunning",
        };
}
