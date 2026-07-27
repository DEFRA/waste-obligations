namespace Defra.WasteObligations.Api.Services.PrnCommonBackend;

public interface IPrnCommonBackendService
{
    Task<IEnumerable<Obligation>> ReadObligations(Guid organisationId, int year, CancellationToken cancellationToken);

    Task<PrnData?> ReadPrn(Guid organisationId, string prnId, CancellationToken cancellationToken);

    Task<PrnSearchResponse> SearchPrns(
        Guid organisationId,
        PrnSearchRequest search,
        CancellationToken cancellationToken
    );

    Task<PrnStatusUpdateResult> UpdatePrnStatus(
        Guid organisationId,
        Guid userId,
        string prnId,
        string status,
        CancellationToken cancellationToken
    );
}
