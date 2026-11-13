using System.Text.Json;
using AwesomeAssertions;
using Defra.WasteObligations.AuditEvents.Logging;
using Elastic.CommonSchema.Serilog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace Defra.WasteObligations.Api.Tests.Utils.Logging;

public class CorrelationContextTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Begin_ShouldRestoreOuterCorrelationAfterNestedEmptyScopeAndException(string? id)
    {
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger>();
        var logScope = Substitute.For<IDisposable>();
        logger.BeginScope(Arg.Any<Dictionary<string, object?>>()).Returns(logScope);

        using (CorrelationContext.Begin(logger, "outer"))
        {
            CorrelationContext.Current.Should().Be("outer");
            Action nested = () =>
            {
                using var scope = CorrelationContext.Begin(logger, id);
                CorrelationContext.Current.Should().BeNull();

                throw new InvalidOperationException("Nested operation failed");
            };

            nested.Should().Throw<InvalidOperationException>();
            CorrelationContext.Current.Should().Be("outer");
        }

        CorrelationContext.Current.Should().BeNull();
        logger
            .Received(1)
            .BeginScope(
                Arg.Is<Dictionary<string, object?>>(x => (string?)x[CorrelationContext.PropertyName] == "outer")
            );
        logger
            .Received(1)
            .BeginScope(Arg.Is<Dictionary<string, object?>>(x => x[CorrelationContext.PropertyName] == null));
        logScope.Received(2).Dispose();
    }

    [Fact]
    public void Begin_ShouldSerializeScopedCorrelationAsEcsTraceId()
    {
        using var output = new StringWriter();
        using var serilog = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(new EcsSink(output))
            .CreateLogger();
        using var factory = new SerilogLoggerFactory(serilog);
        var logger = factory.CreateLogger("BackgroundWorker");

        using (CorrelationContext.Begin(logger, "background-trace"))
            logger.LogInformation("Processing background work");

        using var json = JsonDocument.Parse(output.ToString());
        json.RootElement.GetProperty("trace.id").GetString().Should().Be("background-trace");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Begin_ShouldSuppressNestedEmptyCorrelationInEcsAndRestoreOuterCorrelation(string? id)
    {
        using var output = new StringWriter();
        using var serilog = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(new EcsSink(output))
            .CreateLogger();
        using var factory = new SerilogLoggerFactory(serilog);
        var logger = factory.CreateLogger("BackgroundWorker");

        using (CorrelationContext.Begin(logger, "outer"))
        {
            logger.LogInformation("Before nested scope");
            using (CorrelationContext.Begin(logger, id))
                logger.LogInformation("Inside nested scope");
            logger.LogInformation("After nested scope");
        }

        using (CorrelationContext.Begin(logger, id))
            logger.LogInformation("Without correlation");

        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(4);
        using var before = JsonDocument.Parse(lines[0]);
        using var nested = JsonDocument.Parse(lines[1]);
        using var after = JsonDocument.Parse(lines[2]);
        using var standalone = JsonDocument.Parse(lines[3]);
        before.RootElement.GetProperty("trace.id").GetString().Should().Be("outer");
        nested.RootElement.TryGetProperty("trace.id", out _).Should().BeFalse();
        after.RootElement.GetProperty("trace.id").GetString().Should().Be("outer");
        standalone.RootElement.TryGetProperty("trace.id", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Begin_ShouldIsolateConcurrentOperationsAndRestoreParentContext()
    {
        using var parent = CorrelationContext.Begin(NullLogger.Instance, "parent");
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrivals = 0;

        async Task Process(string traceId)
        {
            using (CorrelationContext.Begin(NullLogger.Instance, traceId))
            {
                if (Interlocked.Increment(ref arrivals) == 2)
                    ready.SetResult();

                await ready.Task.WaitAsync(TestContext.Current.CancellationToken);
                await Task.Yield();
                CorrelationContext.Current.Should().Be(traceId);
            }

            CorrelationContext.Current.Should().Be("parent");
        }

        await Task.WhenAll(
            Task.Run(() => Process("first"), TestContext.Current.CancellationToken),
            Task.Run(() => Process("second"), TestContext.Current.CancellationToken)
        );

        CorrelationContext.Current.Should().Be("parent");
    }

    private sealed class EcsSink(TextWriter output) : ILogEventSink
    {
        public void Emit(LogEvent logEvent) => new EcsTextFormatter().Format(logEvent, output);
    }
}
