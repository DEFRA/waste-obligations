namespace Defra.WasteObligations.Api.Services.PrnCommonBackend;

public interface IPrnCommonBackendService
{
    Task<Obligations?> ReadObligations(Guid organisationId, int year, CancellationToken cancellationToken);
}
