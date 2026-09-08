using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Services.OrganisationObligations;

public interface IOrganisationObligationHistoricalBackfillStore
{
    Task<OrganisationObligationHistoricalBackfillCreation> Create(
        OrganisationObligationHistoricalBackfill backfill,
        CancellationToken cancellationToken
    );
    Task<OrganisationObligationHistoricalBackfill?> GetNextIncomplete(CancellationToken cancellationToken);
    Task<OrganisationObligationHistoricalBackfill[]> GetAll(CancellationToken cancellationToken);
    Task MarkEnqueued(OrganisationObligationHistoricalBackfill backfill, CancellationToken cancellationToken);
    Task Complete(OrganisationObligationHistoricalBackfill backfill, CancellationToken cancellationToken);
}
