using Defra.WasteObligations.Api.Dtos;

namespace Defra.WasteObligations.Api.Services;

public interface IUnsubmittedPollingStatusService
{
    Task<UnsubmittedPollingStatus> Get(CancellationToken cancellationToken);
}
