using Defra.WasteObligations.Api.Dtos;

namespace Defra.WasteObligations.Api.Services;

public interface IUnsubmittedPollingVolumeService
{
    Task<UnsubmittedPollingVolume> Get(CancellationToken cancellationToken);
}
