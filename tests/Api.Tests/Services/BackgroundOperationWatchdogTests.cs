using AwesomeAssertions;
using Defra.WasteObligations.Api.Services;
using Microsoft.Extensions.Time.Testing;

namespace Defra.WasteObligations.Api.Tests.Services;

public class BackgroundOperationWatchdogTests
{
    [Fact]
    public async Task Run_WhenAsynchronousTimeoutNotificationFails_ShouldDrainBeforeReportingTheFailure()
    {
        var time = new FakeTimeProvider();
        var operation = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var notification = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var notified = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var execution = BackgroundOperationWatchdog.Run(
            _ => operation.Task,
            TimeSpan.FromSeconds(1),
            () =>
            {
                notified.TrySetResult();

                return notification.Task;
            },
            TestContext.Current.CancellationToken,
            time
        );
        time.Advance(TimeSpan.FromSeconds(1));
        await notified.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        execution.IsCompleted.Should().BeFalse();
        notification.SetException(new InvalidOperationException("Async cancellation callback failed"));
        operation.SetResult(42);

        var action = () => execution;
        (await action.Should().ThrowAsync<InvalidOperationException>()).Which.InnerException.Should().NotBeNull();
    }

    [Fact]
    public async Task Run_WhenCancellationIsIgnored_ShouldReportTimeoutAndWaitForActualCompletion()
    {
        var time = new FakeTimeProvider();
        var finished = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var alarms = 0;
        var execution = BackgroundOperationWatchdog.Run(
            token =>
            {
                token.Register(() => cancelled.TrySetResult());

                return finished.Task;
            },
            TimeSpan.FromSeconds(1),
            () => alarms++,
            TestContext.Current.CancellationToken,
            time
        );

        time.Advance(TimeSpan.FromSeconds(1));
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        alarms.Should().Be(1);
        execution.IsCompleted.Should().BeFalse();
        finished.SetResult(42);
        (await execution).Should().Be(42);
    }

    [Fact]
    public async Task Run_WhenLeaseAcquisitionCompletesLate_ShouldRequireFreshOwnershipBeforeStartingWork()
    {
        var time = new FakeTimeProvider();
        var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var alarm = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var execution = BackgroundOperationWatchdog.Run(
            token =>
            {
                token.Register(() => cancelled.TrySetResult());

                return finished.Task;
            },
            TimeSpan.FromSeconds(1),
            () => alarm.TrySetResult(),
            TestContext.Current.CancellationToken,
            time,
            rejectLateResult: true
        );
        time.Advance(TimeSpan.FromSeconds(1));
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        execution.IsCompleted.Should().BeFalse();
        alarm.Task.IsCompleted.Should().BeTrue();
        finished.SetResult(true);

        var action = () => execution;
        await action.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task Run_WhenOperationHonoursTimeoutCancellation_ShouldReturnTimeoutFailure()
    {
        var time = new FakeTimeProvider();
        var alarms = 0;
        var execution = BackgroundOperationWatchdog.Run(
            token => Task.Delay(Timeout.InfiniteTimeSpan, token),
            TimeSpan.FromSeconds(1),
            () => alarms++,
            TestContext.Current.CancellationToken,
            time
        );
        time.Advance(TimeSpan.FromSeconds(1));

        var action = () => execution;
        await action.Should().ThrowAsync<TimeoutException>();
        alarms.Should().Be(1);
    }

    [Fact]
    public async Task Run_WhenHostStops_ShouldCancelWithoutReportingAnOperationTimeout()
    {
        var alarms = 0;
        using var stopping = new CancellationTokenSource();
        var execution = BackgroundOperationWatchdog.Run(
            token => Task.Delay(Timeout.InfiniteTimeSpan, token),
            TimeSpan.FromMinutes(1),
            () => alarms++,
            stopping.Token
        );
        await stopping.CancelAsync();

        var action = () => execution;
        await action.Should().ThrowAsync<OperationCanceledException>();
        alarms.Should().Be(0);
    }

    [Fact]
    public async Task Run_WhenCallerCancellationIsIgnored_ShouldStillReportTheStalledOperation()
    {
        var time = new FakeTimeProvider();
        using var caller = new CancellationTokenSource();
        var finished = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var alarm = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var execution = BackgroundOperationWatchdog.Run(
            _ => finished.Task,
            TimeSpan.FromSeconds(1),
            () => alarm.TrySetResult(),
            caller.Token,
            time
        );
        await caller.CancelAsync();
        time.Advance(TimeSpan.FromSeconds(1));
        await alarm.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        execution.IsCompleted.Should().BeFalse();

        finished.SetResult(42);

        (await execution).Should().Be(42);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Run_WhenTimeoutNotificationThrows_ShouldStillDrainTheOriginalOperation(bool reportingFails)
    {
        var time = new FakeTimeProvider();
        var finished = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var execution = BackgroundOperationWatchdog.Run(
            token =>
            {
                token.Register(() =>
                {
                    cancelled.TrySetResult();
                    if (!reportingFails)
                        throw new InvalidOperationException("Cancellation notification failed");
                });

                return finished.Task;
            },
            TimeSpan.FromSeconds(1),
            () =>
            {
                if (reportingFails)
                    throw new InvalidOperationException("Timeout reporting failed");
            },
            TestContext.Current.CancellationToken,
            time
        );
        time.Advance(TimeSpan.FromSeconds(1));
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var quietPeriod = Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        (await Task.WhenAny(execution, quietPeriod)).Should().BeSameAs(quietPeriod);

        finished.SetResult(42);

        var action = () => execution;
        (await action.Should().ThrowAsync<InvalidOperationException>()).Which.InnerException.Should().NotBeNull();
    }

    [Fact]
    public async Task Run_WhenAlreadyCancelled_ShouldNotInvokeTheOperation()
    {
        using var caller = new CancellationTokenSource();
        await caller.CancelAsync();
        var started = false;
        var execution = BackgroundOperationWatchdog.Run(
            _ =>
            {
                started = true;

                return Task.CompletedTask;
            },
            TimeSpan.FromSeconds(1),
            () => started = true,
            caller.Token
        );

        var action = () => execution;
        await action.Should().ThrowAsync<OperationCanceledException>();
        started.Should().BeFalse();
    }
}
