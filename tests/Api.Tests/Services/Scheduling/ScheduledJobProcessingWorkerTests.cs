using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.Scheduling;
using Defra.WasteObligations.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Services.Scheduling;

public class ScheduledJobProcessingWorkerTests
{
    [Fact]
    public async Task Start_WhenDisabled_ShouldNotFindOrProcessWork()
    {
        var source = Substitute.For<IScheduledJobWorkSource<ObligationCalculationInstruction>>();
        var processor = Substitute.For<IScheduledJobProcessor<ObligationCalculationInstruction>>();
        using var services = Services(source, processor);
        using var worker = Worker(services, new ScheduledJobProcessingOptions(), _ => Lease());

        await worker.StartAsync(TestContext.Current.CancellationToken);
        worker.ExecuteTask.Should().NotBeNull();
        await worker.ExecuteTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await source.DidNotReceive().GetNext(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
        await processor
            .DidNotReceive()
            .Process(Arg.Any<ObligationCalculationInstruction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_ShouldSkipBusyWorkAndPassAvailablePayloadToProcessorBeforeCompleting()
    {
        var first = new ScheduledJobWork<ObligationCalculationInstruction>(
            "busy",
            new() { ObligationYear = 2026, CreatedAt = DateTime.UtcNow }
        );
        var second = new ScheduledJobWork<ObligationCalculationInstruction>(
            "available",
            new() { ObligationYear = 2025, CreatedAt = DateTime.UtcNow }
        );
        var source = Substitute.For<IScheduledJobWorkSource<ObligationCalculationInstruction>>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var processed = false;
        source
            .GetNext(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                ScheduledJobWork<ObligationCalculationInstruction>? next = null;
                if (!processed)
                    next = call.Arg<IReadOnlyCollection<string>>().Contains(first.Id) ? second : first;

                return Task.FromResult(next);
            });
        source.TryStart(second, Arg.Any<CancellationToken>()).Returns(true);
        source
            .Complete(second, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                processed.Should().BeTrue();
                completed.TrySetResult();

                return Task.CompletedTask;
            });
        var processor = Substitute.For<IScheduledJobProcessor<ObligationCalculationInstruction>>();
        processor
            .Process(second.Payload, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                processed = true;

                return Task.CompletedTask;
            });
        var busy = Lease();
        busy.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(false);
        using var services = Services(source, processor);
        using var worker = Worker(
            services,
            new ScheduledJobProcessingOptions { Enabled = true, PollIntervalSeconds = 3600 },
            id => id == first.Id ? busy : Lease()
        );

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await worker.StopAsync(TestContext.Current.CancellationToken);

        await processor.Received(1).Process(second.Payload, Arg.Any<CancellationToken>());
        await processor.DidNotReceive().Process(first.Payload, Arg.Any<CancellationToken>());
        await source.DidNotReceive().TryStart(first, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_WhenWorkDiscoveryFails_ShouldLogAndRetryAfterPollingInterval()
    {
        var source = Substitute.For<IScheduledJobWorkSource<ObligationCalculationInstruction>>();
        var failure = new InvalidOperationException("Discovery failed");
        var retried = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source
            .GetNext(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromException<ScheduledJobWork<ObligationCalculationInstruction>?>(failure),
                _ =>
                {
                    retried.TrySetResult();

                    return Task.FromResult<ScheduledJobWork<ObligationCalculationInstruction>?>(null);
                }
            );
        var processor = Substitute.For<IScheduledJobProcessor<ObligationCalculationInstruction>>();
        var logger = new RecordingLogger<ScheduledJobProcessingWorker<ObligationCalculationInstruction>>();
        using var services = Services(source, processor);
        using var worker = new ScheduledJobProcessingWorker<ObligationCalculationInstruction>(
            "test",
            services.GetRequiredService<IServiceScopeFactory>(),
            new() { Enabled = true, PollIntervalSeconds = 1 },
            TimeProvider.System,
            _ => Lease(),
            logger
        );

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await retried.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await worker.StopAsync(TestContext.Current.CancellationToken);

        logger.Entries.Should().ContainSingle(x => ReferenceEquals(x.Exception, failure));
        await processor
            .DidNotReceive()
            .Process(Arg.Any<ObligationCalculationInstruction>(), Arg.Any<CancellationToken>());
    }

    private static ServiceProvider Services(
        IScheduledJobWorkSource<ObligationCalculationInstruction> source,
        IScheduledJobProcessor<ObligationCalculationInstruction> processor
    ) => new ServiceCollection().AddScoped(_ => source).AddScoped(_ => processor).BuildServiceProvider();

    private static ScheduledJobProcessingWorker<ObligationCalculationInstruction> Worker(
        ServiceProvider services,
        ScheduledJobProcessingOptions options,
        Func<string, IScheduledJobLeaseService> leases
    ) =>
        new(
            "test",
            services.GetRequiredService<IServiceScopeFactory>(),
            options,
            TimeProvider.System,
            leases,
            new RecordingLogger<ScheduledJobProcessingWorker<ObligationCalculationInstruction>>()
        );

    private static IScheduledJobLeaseService Lease()
    {
        var lease = Substitute.For<IScheduledJobLeaseService>();
        lease.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        lease.TryRenew(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);

        return lease;
    }
}
