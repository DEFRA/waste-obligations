namespace Defra.WasteObligations.Api.Services.Scheduling;

public interface IScheduledJobWorkSource<TWork>
{
    Task<ScheduledJobWork<TWork>?> GetNext(
        IReadOnlyCollection<string> excludedIds,
        CancellationToken cancellationToken
    );

    Task<bool> TryStart(ScheduledJobWork<TWork> work, CancellationToken cancellationToken);

    Task Complete(ScheduledJobWork<TWork> work, CancellationToken cancellationToken);
}
