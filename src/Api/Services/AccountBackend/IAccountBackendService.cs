namespace Defra.WasteObligations.Api.Services.AccountBackend;

public interface IAccountBackendService
{
    Task<OrganisationsByExternalIdsResponse> SearchOrganisationsByExternalIds(
        IReadOnlyCollection<Guid> externalIds,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<AccountOrganisation>> SearchOrganisationsByCompaniesHouseNumbers(
        IReadOnlyCollection<string> companiesHouseNumbers,
        CancellationToken cancellationToken
    );

    Task<OrganisationWithPersons?> ReadOrganisationWithPersons(
        Guid organisationId,
        CancellationToken cancellationToken
    );
}
