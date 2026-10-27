using AwesomeAssertions;
using Defra.WasteObligations.Api.Services;

namespace Defra.WasteObligations.Api.Tests.Services;

public class BackgroundOperationCleanupTests
{
    [Fact]
    public async Task Run_WhenCleanupFails_ShouldDrainRemainingStepsBeforeReportingAllFailures()
    {
        var first = new InvalidOperationException("Cancellation callback failed");
        var second = new InvalidOperationException("Renewal failed");
        var renewal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = false;
        var execution = BackgroundOperationCleanup.Run(
            () => Task.FromException(first),
            () => renewal.Task,
            () =>
            {
                released = true;

                return Task.CompletedTask;
            }
        );

        released.Should().BeFalse();
        execution.IsCompleted.Should().BeFalse();
        renewal.SetException(second);

        var action = () => execution;
        (await action.Should().ThrowAsync<AggregateException>()).Which.InnerExceptions.Should().Equal(first, second);
        released.Should().BeTrue();
    }
}
