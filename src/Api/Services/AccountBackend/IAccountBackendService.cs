namespace Defra.WasteObligations.Api.Services.AccountBackend;

public interface IAccountBackendService
{
    Task<IEnumerable<PersonEmail>> ReadPersonEmails(
        Guid organisationId,
        EntityTypeCode entityTypeCode,
        CancellationToken cancellationToken
    );

    Task<OrganisationWithPersons?> ReadOrganisationWithPersons(
        Guid organisationId,
        CancellationToken cancellationToken
    );
}
