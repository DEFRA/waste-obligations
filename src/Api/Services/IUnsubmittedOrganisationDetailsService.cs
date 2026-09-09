using Defra.WasteObligations.Api.Dtos;

namespace Defra.WasteObligations.Api.Services;

public interface IUnsubmittedOrganisationDetailsService
{
    Task<UnsubmittedOrganisationDetails?> Get(
        Guid organisationId,
        bool includeLiveData,
        CancellationToken cancellationToken
    );
}
