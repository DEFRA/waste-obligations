namespace Defra.WasteObligations.Api.Services.WasteOrganisations;

public interface IWasteOrganisationsService
{
    Task<Organisation?> Read(Guid organisationId, CancellationToken cancellationToken);
}
