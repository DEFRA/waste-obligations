using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services.Scheduling;

public interface IScheduledJob
{
    Task Run(IClientSessionHandle session, CancellationToken cancellationToken);
}
