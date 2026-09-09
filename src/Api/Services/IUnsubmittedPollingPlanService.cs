using Defra.WasteObligations.Api.Dtos;

namespace Defra.WasteObligations.Api.Services;

public interface IUnsubmittedPollingPlanService
{
    Task<UnsubmittedPollingPlan> Get(CancellationToken cancellationToken);
}
