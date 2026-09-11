namespace Defra.WasteObligations.Api.Services.OrganisationObligations;

public interface IOrganisationObligationRequestPacer
{
    Task ObserveWorkload(int activeSummaryCount, CancellationToken cancellationToken);

    Task ObserveWorkload(int activeSummaryCount, int dueSummaryCount, CancellationToken cancellationToken);

    Task ObserveRead(TimeSpan duration, bool succeeded, CancellationToken cancellationToken);

    Task<OrganisationObligationRequestPacingStatus> GetStatus(CancellationToken cancellationToken);

    Task Wait(CancellationToken cancellationToken);
}
