namespace Defra.WasteObligations.Api.Services.PrnCommonBackend;

public interface IPrnCommonBackendService
{
    Task<IEnumerable<Obligation>> ReadObligations(Guid organisationId, int year, CancellationToken cancellationToken);

    Task<PrnDetails?> ReadPrn(Guid organisationId, string prnId, CancellationToken cancellationToken);
}
