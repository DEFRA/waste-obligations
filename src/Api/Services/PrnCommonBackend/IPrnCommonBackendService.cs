namespace Defra.WasteObligations.Api.Services.PrnCommonBackend;

public interface IPrnCommonBackendService
{
    Task<IEnumerable<Obligation>> ReadObligations(Guid organisationId, int year, CancellationToken cancellationToken);
}
