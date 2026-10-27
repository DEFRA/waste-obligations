namespace Defra.WasteObligations.Api.Services;

public static class BackgroundOperationWatchdog
{
    public static Task Run(
        Func<CancellationToken, Task> operation,
        TimeSpan timeout,
        Action timedOut,
        CancellationToken cancellationToken,
        TimeProvider? timeProvider = null
    ) =>
        Run(
            async token =>
            {
                await operation(token);
                return true;
            },
            timeout,
            timedOut,
            cancellationToken,
            timeProvider
        );

    public static async Task<T> Run<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan timeout,
        Action timedOut,
        CancellationToken cancellationToken,
        TimeProvider? timeProvider = null,
        bool rejectLateResult = false
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var work = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var timer = new CancellationTokenSource();
        var elapsed = false;
        Exception? notificationFailure = null;
        try
        {
            var deadline = Task.Delay(timeout, timeProvider ?? TimeProvider.System, timer.Token);
            var task = operation(work.Token);
            elapsed = await Task.WhenAny(task, deadline) != task;
            if (elapsed)
            {
                try
                {
                    timedOut();
                }
                catch (Exception exception)
                {
                    notificationFailure = exception;
                }
                try
                {
                    await work.CancelAsync();
                }
                catch (Exception exception)
                {
                    notificationFailure = notificationFailure is null
                        ? exception
                        : new AggregateException(notificationFailure, exception);
                }
            }

            // Do not abandon a command which may still mutate data or own a lease.
            T result;
            try
            {
                result = await task;
            }
            catch (Exception exception) when (notificationFailure is not null)
            {
                throw new AggregateException(
                    "Timeout notification failed and the operation also failed after draining.",
                    notificationFailure,
                    exception
                );
            }
            if (notificationFailure is not null)
                throw new InvalidOperationException(
                    "Timeout notification failed; the original operation has now finished.",
                    notificationFailure
                );
            if (elapsed && rejectLateResult)
                throw new TimeoutException(
                    "A lease acquisition completed after its cancellation threshold; ownership must be reacquired before work starts."
                );

            return result;
        }
        catch (OperationCanceledException exception) when (elapsed && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Background operation exceeded its {timeout} cancellation threshold.",
                exception
            );
        }
        finally
        {
            await timer.CancelAsync();
        }
    }
}
