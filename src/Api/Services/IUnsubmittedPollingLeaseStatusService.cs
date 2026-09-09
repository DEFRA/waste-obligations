using Defra.WasteObligations.Api.Dtos;

namespace Defra.WasteObligations.Api.Services;

public interface IUnsubmittedPollingLeaseStatusService
{
    Task<UnsubmittedPollingLeaseStatus> Get(CancellationToken cancellationToken);
}
