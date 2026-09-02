namespace Defra.WasteObligations.Api.Services.AccountBackend;

public interface IAccountBackendService
{
    Task<OrganisationWithPersons?> ReadOrganisationWithPersons(
        Guid organisationId,
        CancellationToken cancellationToken
    );
}
