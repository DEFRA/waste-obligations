namespace Defra.WasteObligations.Api.Services.AccountBackend;

public interface IOrganisationReferenceSearchService
{
    Task<OrganisationsByExternalIdsResponse> SearchOrganisationsByExternalIds(
        IReadOnlyCollection<Guid> externalIds,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<AccountOrganisation>> SearchOrganisationsByCompaniesHouseNumbers(
        IReadOnlyCollection<string> companiesHouseNumbers,
        CancellationToken cancellationToken
    );
}
