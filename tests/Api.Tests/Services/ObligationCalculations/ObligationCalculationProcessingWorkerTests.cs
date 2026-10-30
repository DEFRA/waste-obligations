using AwesomeAssertions;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using Defra.WasteObligations.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Services.ObligationCalculations;

public class ObligationCalculationProcessingWorkerTests
{
    [Fact]
    public async Task Start_WhenDisabled_ShouldNotProcessInstructions()
    {
        var processor = Substitute.For<IObligationCalculationInstructionProcessor>();
        using var services = Services(processor);
        using var worker = CreateWorker(services, new ObligationCalculationProcessingOptions());

        await worker.StartAsync(TestContext.Current.CancellationToken);
        worker.ExecuteTask.Should().NotBeNull();
        await worker.ExecuteTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await worker.StopAsync(TestContext.Current.CancellationToken);

        await processor.DidNotReceive().ProcessNext(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_ShouldDrainAvailableInstructionsBeforeWaiting()
    {
        var processor = Substitute.For<IObligationCalculationInstructionProcessor>();
        var drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        processor
            .ProcessNext(Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromResult(true),
                _ =>
                {
                    drained.TrySetResult();

                    return Task.FromResult(false);
                }
            );
        using var services = Services(processor);
        using var worker = CreateWorker(
            services,
            new ObligationCalculationProcessingOptions { Enabled = true, PollIntervalSeconds = 3600 }
        );

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await drained.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await worker.StopAsync(TestContext.Current.CancellationToken);

        await processor.Received(2).ProcessNext(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_WhenProcessingFails_ShouldLogAndRetryAfterPollingInterval()
    {
        var processor = Substitute.For<IObligationCalculationInstructionProcessor>();
        var failure = new InvalidOperationException("Processing failed");
        var retried = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        processor
            .ProcessNext(Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromException<bool>(failure),
                _ =>
                {
                    retried.TrySetResult();

                    return Task.FromResult(false);
                }
            );
        var logger = new RecordingLogger<ObligationCalculationProcessingWorker>();
        using var services = Services(processor);
        using var worker = new ObligationCalculationProcessingWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ObligationCalculationProcessingOptions { Enabled = true, PollIntervalSeconds = 1 }),
            logger
        );

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await retried.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await worker.StopAsync(TestContext.Current.CancellationToken);

        logger.Entries.Should().ContainSingle(x => ReferenceEquals(x.Exception, failure));
        await processor.Received(2).ProcessNext(Arg.Any<CancellationToken>());
    }

    private static ServiceProvider Services(IObligationCalculationInstructionProcessor processor) =>
        new ServiceCollection().AddScoped(_ => processor).BuildServiceProvider();

    private static ObligationCalculationProcessingWorker CreateWorker(
        ServiceProvider services,
        ObligationCalculationProcessingOptions options
    ) =>
        new(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options),
            new RecordingLogger<ObligationCalculationProcessingWorker>()
        );
}
