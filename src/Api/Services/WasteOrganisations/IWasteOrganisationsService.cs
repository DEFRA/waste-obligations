namespace Defra.WasteObligations.Api.Services.WasteOrganisations;

public interface IWasteOrganisationsService : IOrganisationEligibilitySource
{
    Task<Organisation?> Read(Guid organisationId, CancellationToken cancellationToken);
}
