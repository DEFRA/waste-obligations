using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Services.OrganisationObligations;

public interface IOrganisationObligationHydrationService
{
    Task<int> EnqueueNewEligible(int obligationYear, CancellationToken cancellationToken);
    Task<OrganisationObligationHistoricalBackfillProgress> HydrateHistoricalBackfill(
        OrganisationObligationHistoricalBackfill backfill,
        CancellationToken cancellationToken,
        int? maximumWork = null,
        bool preserveCurrentYearPacing = false
    );
    Task<int> HydrateDue(int obligationYear, CancellationToken cancellationToken, int? maximumWork = null);
}
