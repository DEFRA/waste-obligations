using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Api.Services.WasteOrganisations;

namespace Defra.WasteObligations.Api.Services;

public class OrganisationService(IPrnCommonBackendService prnCommonBackendService) : IOrganisationService
{
    /// <summary>
    /// Until we integrate with waste-organisations, we just echo back the organisation
    /// from this call as if it were found.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<Organisation?> ReadOrganisation(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<Organisation?>(
            new Organisation
            {
                Id = id,
                Name = "name",
                Address = new Address(),
            }
        );

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
