using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.ScheduledJobs;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using Defra.WasteObligations.Api.Services.WastePackaging;
using Defra.WasteObligations.Api.Utils.Metrics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Services.ObligationCalculations;

public class ObligationCalculationProcessorMetricsTests
{
    [Fact]
    public async Task FetchFailure_RecordsDurationAndFailureWithoutSubmitterOutcomes()
    {
        var time = new FakeTimeProvider();
        var source = Substitute.For<IWastePackagingService>();
        var metrics = Substitute.For<IObligationCalculationMetrics>();
        source
            .Read(2025, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                time.Advance(TimeSpan.FromSeconds(3));
                throw new HttpRequestException();
            });
        var subject = Processor(source, metrics, time);

        await Assert.ThrowsAsync<HttpRequestException>(() => subject.Process(Instruction(), CancellationToken.None));

        metrics.Received(1).FetchDuration(TimeSpan.FromSeconds(3));
        metrics.Received(1).Instruction(CalculationInstructionOutcome.PackagingFailure);
        metrics.DidNotReceive().Submitter(Arg.Any<CalculationSaveOutcome>());
        metrics.DidNotReceive().SubmitterFailed();
    }

    [Fact]
    public async Task FetchCancellation_RecordsDurationWithoutInstructionOutcome()
    {
        var time = new FakeTimeProvider();
        var source = Substitute.For<IWastePackagingService>();
        var metrics = Substitute.For<IObligationCalculationMetrics>();
        using var cancellation = new CancellationTokenSource();
        source
            .Read(2025, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                time.Advance(TimeSpan.FromSeconds(4));
                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            });
        var subject = Processor(source, metrics, time);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            subject.Process(Instruction(), cancellation.Token)
        );

        metrics.Received(1).FetchDuration(TimeSpan.FromSeconds(4));
        metrics.DidNotReceive().Instruction(Arg.Any<CalculationInstructionOutcome>());
    }

    private static ObligationCalculationProcessor Processor(
        IWastePackagingService source,
        IObligationCalculationMetrics metrics,
        TimeProvider time
    ) =>
        new(
            source,
            Substitute.For<IObligationCalculator>(),
            Substitute.For<IObligationCalculationService>(),
            metrics,
            time,
            NullLogger<ObligationCalculationProcessor>.Instance
        );

    private static ObligationCalculationInstruction Instruction() =>
        new() { ObligationYear = 2026, CreatedAt = DateTime.UtcNow };
}
