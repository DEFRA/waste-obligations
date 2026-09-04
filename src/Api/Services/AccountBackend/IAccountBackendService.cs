namespace Defra.WasteObligations.Api.Services.AccountBackend;

public interface IAccountBackendService : IOrganisationReferenceSearchService
{
    Task<OrganisationWithPersons?> ReadOrganisationWithPersons(
        Guid organisationId,
        CancellationToken cancellationToken
    );
}
