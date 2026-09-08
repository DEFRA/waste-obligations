using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Services.OrganisationObligations;

public interface IOrganisationObligationHydrationService
{
    Task<int> EnqueueNewEligible(int obligationYear, CancellationToken cancellationToken);

    Task<int> EnqueueReconciliation(
        int obligationYear,
        DateTime reconciliationSince,
        CancellationToken cancellationToken
    );

    Task<OrganisationObligationHydrationPreparedWork> PrepareDueWork(
        int obligationYear,
        CancellationToken cancellationToken
    );

    Task<int> HydratePreparedDueWork(
        OrganisationObligationHydrationPreparedWork work,
        CancellationToken cancellationToken,
        int? maximumWork = null,
        bool deactivateAfterSuccessfulRead = false
    );

    Task<OrganisationObligationHistoricalBackfillProgress> HydrateHistoricalBackfill(
        OrganisationObligationHistoricalBackfill backfill,
        CancellationToken cancellationToken,
        int? maximumWork = null,
        bool preserveCurrentYearPacing = false
    );
    Task<int> HydrateDue(int obligationYear, CancellationToken cancellationToken, int? maximumWork = null);
}
