using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Api.Services.WasteOrganisations;

namespace Defra.WasteObligations.Api.Services;

public class OrganisationService(
    IPrnCommonBackendService prnCommonBackendService,
    IWasteOrganisationsService wasteOrganisationsService
) : IOrganisationService
{
    public async Task<Organisation?> ReadOrganisation(Guid id, CancellationToken cancellationToken) =>
        await wasteOrganisationsService.ReadOrganisation(id, cancellationToken);

    public async Task<IEnumerable<Obligation>> ReadObligations(
        Guid organisationId,
        int year,
        CancellationToken cancellationToken
    )
    {
        var obligations = await prnCommonBackendService.ReadObligations(organisationId, year, cancellationToken);

        return obligations is not null ? obligations.ObligationData : [];
    }
}
