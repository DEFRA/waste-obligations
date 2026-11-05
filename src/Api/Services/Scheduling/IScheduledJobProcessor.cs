namespace Defra.WasteObligations.Api.Services.Scheduling;

public interface IScheduledJobProcessor<in TWork>
{
    Task Process(TWork work, CancellationToken cancellationToken);
}
