using System.Net;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.ScheduledJobs;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using Defra.WasteObligations.Api.Services.WastePackaging;
using Defra.WasteObligations.Api.Utils.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Services.ObligationCalculations;

public class ObligationCalculationProcessorMetricsTests
{
    [Fact]
    public async Task FetchDuration_IncludesResilienceRetryAndResponseReading()
    {
        var time = new FakeTimeProvider();
        var handler = new RecoveringHandler(time);
        var settings = new Dictionary<string, string?>
        {
            ["WastePackaging:BaseAddress"] = "http://localhost",
            ["WastePackaging:ClientId"] = "client",
            ["WastePackaging:ClientSecret"] = "secret",
            ["WastePackaging:Retry:MaxRetryAttempts"] = "1",
            ["WastePackaging:Retry:Delay"] = "00:00:00.01",
            ["WastePackaging:Retry:BackoffType"] = "Constant",
            ["WastePackaging:Retry:UseJitter"] = "false",
        };
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddWastePackagingService();
        services.Configure<HttpStandardResilienceOptions>(
            "WastePackaging",
            options =>
                options.Retry.OnRetry = _ =>
                {
                    time.Advance(TimeSpan.FromSeconds(5));
                    return ValueTask.CompletedTask;
                }
        );
        services
            .AddHttpClient<IWastePackagingService, WastePackagingService>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        using var provider = services.BuildServiceProvider();
        var metrics = Substitute.For<IObligationCalculationMetrics>();
        var subject = Processor(provider.GetRequiredService<IWastePackagingService>(), metrics, time);

        await subject.Process(Instruction(), CancellationToken.None);

        Assert.Equal(2, handler.RequestCount);
        metrics.Received(1).FetchDuration(TimeSpan.FromSeconds(7));
        metrics.Received(1).Instruction(CalculationInstructionOutcome.Success);
        metrics.DidNotReceive().Submitter(Arg.Any<CalculationSaveOutcome>());
    }

    private sealed class RecoveringHandler(FakeTimeProvider time) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            time.Advance(TimeSpan.FromSeconds(1));
            RequestCount++;
            return Task.FromResult(
                RequestCount == 1
                    ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"items\":[]}", System.Text.Encoding.UTF8, "application/json"),
                    }
            );
        }
    }

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
                return Task.FromException<PackagingResponse>(new HttpRequestException());
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
                return Task.FromCanceled<PackagingResponse>(cancellation.Token);
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
