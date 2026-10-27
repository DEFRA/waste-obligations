namespace Defra.WasteObligations.Api.Data;

public class MongoMigrationCompletion
{
    private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Complete() => _completed.TrySetResult();

    public void Fail(Exception exception) => _completed.TrySetException(exception);

    public Task Wait(CancellationToken cancellationToken) => _completed.Task.WaitAsync(cancellationToken);
}
