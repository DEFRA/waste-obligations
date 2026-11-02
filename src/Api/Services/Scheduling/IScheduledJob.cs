namespace Defra.WasteObligations.Api.Services.Scheduling;

public interface IScheduledJob
{
    Task Run(CancellationToken cancellationToken);
}
