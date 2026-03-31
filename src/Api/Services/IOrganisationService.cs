using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Api.Services.WasteOrganisations;

namespace Defra.WasteObligations.Api.Services;

public interface IOrganisationService
{
    Task<Organisation?> GetOrganisation(Guid id, CancellationToken cancellationToken);

    Task<IEnumerable<Obligation>> GetObligations(Guid organisationId, int year, CancellationToken cancellationToken);
}
