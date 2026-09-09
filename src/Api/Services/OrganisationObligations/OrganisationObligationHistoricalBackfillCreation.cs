using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Services.OrganisationObligations;

public record OrganisationObligationHistoricalBackfillCreation
{
    public required OrganisationObligationHistoricalBackfill Backfill { get; init; }
    public required bool WasCreated { get; init; }
}
