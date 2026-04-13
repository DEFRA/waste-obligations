namespace Defra.WasteObligations.Api.Services.WasteOrganisations;

public interface IWasteOrganisationsService
{
    Task<Organisation?> ReadOrganisation(Guid organisationId, CancellationToken cancellationToken);
}
