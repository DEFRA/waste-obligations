using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Api.Services.WasteOrganisations;

namespace Defra.WasteObligations.Api.Services;

public interface IOrganisationService
{
    Task<Organisation?> ReadOrganisation(Guid id, CancellationToken cancellationToken);

    Task<IEnumerable<Obligation>> ReadObligations(Guid organisationId, int year, CancellationToken cancellationToken);
}
