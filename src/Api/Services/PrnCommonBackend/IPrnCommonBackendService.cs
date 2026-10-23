namespace Defra.WasteObligations.Api.Services.PrnCommonBackend;

public interface IPrnCommonBackendService : IOrganisationObligationSource
{
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

    Task<PrnStatusUpdateResult> UpdatePrnStatuses(
        Guid organisationId,
        Guid userId,
        IReadOnlyCollection<Guid> prnIds,
        string status,
        int? obligationYear,
        string? expectedStatus,
        CancellationToken cancellationToken
    );
}
