using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Defra.WasteObligations.Api.Services.Scheduling;

internal sealed class ScheduledJobExecutionRunner(
    string name,
    IScheduledJobLeaseService leaseService,
    TimeProvider timeProvider,
    ILogger logger
)
{
    private static readonly TimeSpan s_leaseOperationTimeout = TimeSpan.FromSeconds(10);
    private readonly TimeSpan _leaseDuration = TimeSpan.FromMinutes(1);

    public async Task<bool> Run(
        IServiceScopeFactory scopeFactory,
        Func<IServiceProvider, CancellationToken, Task> operation,
        CancellationToken stoppingToken
    )
    {
        if (!await TryAcquireLease(stoppingToken))
            return false;

        using var work = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        using var renewal = new CancellationTokenSource();
        var renewalTask = RenewLease(work, renewal.Token);
        Exception? processingFailure = null;
        AsyncServiceScope? scope = null;

        try
        {
            scope = scopeFactory.CreateAsyncScope();
            work.Token.ThrowIfCancellationRequested();
            await operation(scope.Value.ServiceProvider, work.Token);
        }
        catch (Exception exception)
        {
            processingFailure = exception;
        }

        try
        {
            await BackgroundOperationCleanup.Run(
                () => scope is { } createdScope ? createdScope.DisposeAsync().AsTask() : Task.CompletedTask,
                work.CancelAsync,
                renewal.CancelAsync,
                () => renewalTask,
                ReleaseLease
            );
        }
        catch (Exception exception) when (processingFailure is not null)
        {
            throw new AggregateException("Scheduled job execution and cleanup failed.", processingFailure, exception);
        }

        if (processingFailure is not null)
            ExceptionDispatchInfo.Capture(processingFailure).Throw();

        return true;
    }

    private async Task RenewLease(CancellationTokenSource work, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20), timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (
                    await BackgroundOperationWatchdog.Run(
                        token => leaseService.TryRenew(_leaseDuration, token),
                        s_leaseOperationTimeout,
                        async () =>
                        {
                            logger.LogError(
                                "Scheduled job {JobName} lease renewal stalled; cancelling work and draining the call",
                                name
                            );
                            await work.CancelAsync();
                        },
                        cancellationToken,
                        timeProvider,
                        rejectLateResult: true
                    )
                )
                    continue;

                logger.LogError("Scheduled job {JobName} lost its lease", name);
                await work.CancelAsync();

                return;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Renewal stops after the job has finished.
        }
        catch (TimeoutException) when (work.IsCancellationRequested)
        {
            // The watchdog already cancelled the job and reported the stalled renewal.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Scheduled job {JobName} lease renewal failed", name);
            await work.CancelAsync();
        }
    }

    private async Task<bool> TryAcquireLease(CancellationToken cancellationToken)
    {
        var acquired = false;
        try
        {
            return await BackgroundOperationWatchdog.Run(
                async token =>
                {
                    acquired = await leaseService.TryAcquire(_leaseDuration, token);
                    token.ThrowIfCancellationRequested();

                    return acquired;
                },
                s_leaseOperationTimeout,
                () =>
                    logger.LogWarning(
                        "Scheduled job {JobName} lease acquisition stalled; cancelling and draining the call",
                        name
                    ),
                cancellationToken,
                timeProvider,
                rejectLateResult: true
            );
        }
        catch
        {
            if (acquired)
                await ReleaseLease();

            throw;
        }
    }

    private async Task ReleaseLease()
    {
        try
        {
            await BackgroundOperationWatchdog.Run(
                leaseService.Release,
                s_leaseOperationTimeout,
                () =>
                    logger.LogWarning(
                        "Scheduled job {JobName} lease release stalled; cancelling and draining the call",
                        name
                    ),
                CancellationToken.None,
                timeProvider
            );
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Scheduled job {JobName} lease could not be released; it will expire automatically",
                name
            );
        }
    }
}
