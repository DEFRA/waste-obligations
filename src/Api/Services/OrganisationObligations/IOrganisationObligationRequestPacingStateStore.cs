using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Services.OrganisationObligations;

public interface IOrganisationObligationRequestPacingStateStore
{
    Task<OrganisationObligationRequestPacingState?> Get(CancellationToken cancellationToken);

    Task<OrganisationObligationRequestPacingState> Update(
        Func<OrganisationObligationRequestPacingState, OrganisationObligationRequestPacingState> update,
        CancellationToken cancellationToken
    );
}
