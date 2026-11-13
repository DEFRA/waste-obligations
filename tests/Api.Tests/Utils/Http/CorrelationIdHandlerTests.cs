using System.Collections.Concurrent;
using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Utils.Http;
using Defra.WasteObligations.Api.Utils.Logging;
using Defra.WasteObligations.AuditEvents.Logging;
using Microsoft.AspNetCore.HeaderPropagation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Defra.WasteObligations.Api.Tests.Utils.Http;

public class CorrelationIdHandlerTests
{
    private const string HeaderName = "x-custom-correlation-id";

    [Theory]
    [InlineData(null, "background")]
    [InlineData("explicit", "explicit")]
    public async Task Send_ShouldUseConfiguredHeaderAndPreserveExplicitCorrelation(string? explicitId, string expected)
    {
        using var sink = new RecordingHandler();
        using var handler = new CorrelationIdHandler(Options.Create(new TraceHeader { Name = HeaderName }))
        {
            InnerHandler = sink,
        };
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/");
        if (explicitId is not null)
            request.Headers.Add(HeaderName, explicitId);
        using var scope = CorrelationContext.Begin(NullLogger.Instance, "background");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        sink.Requests.Should().ContainSingle().Which.TraceId.Should().Be(expected);
    }

    [Fact]
    public async Task Send_WithNullScope_ShouldSuppressOuterCorrelationWithoutAddingHeader()
    {
        using var sink = new RecordingHandler();
        using var handler = new CorrelationIdHandler(Options.Create(new TraceHeader { Name = HeaderName }))
        {
            InnerHandler = sink,
        };
        using var client = new HttpClient(handler);
        using var parent = CorrelationContext.Begin(NullLogger.Instance, "outer");

        using (CorrelationContext.Begin(NullLogger.Instance, null))
        using (var response = await client.GetAsync("https://example.test/", TestContext.Current.CancellationToken))
            sink.Requests.Should().ContainSingle().Which.TraceId.Should().BeNull();

        CorrelationContext.Current.Should().Be("outer");
    }

    [Fact]
    public async Task Send_ShouldPreserveFrameworkPropagatedHeader()
    {
        using var sink = new RecordingHandler();
        var services = new ServiceCollection();
        services.Configure<TraceHeader>(x => x.Name = HeaderName);
        services.AddHeaderPropagation(x => x.Headers.Add(HeaderName));
        services
            .AddHttpClient("correlated")
            .ConfigurePrimaryHttpMessageHandler(() => sink)
            .AddHeaderPropagation()
            .AddCorrelationIdHandler();
        await using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<HeaderPropagationValues>().Headers = new Dictionary<string, StringValues>
        {
            [HeaderName] = "incoming",
        };
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("correlated");
        using var scope = CorrelationContext.Begin(NullLogger.Instance, "background");

        using var response = await client.GetAsync("https://example.test/", TestContext.Current.CancellationToken);

        sink.Requests.Should().ContainSingle().Which.TraceId.Should().Be("incoming");
    }

    [Fact]
    public async Task Send_ShouldKeepConcurrentBackgroundHeadersIsolated()
    {
        using var sink = new RecordingHandler();
        using var handler = new CorrelationIdHandler(Options.Create(new TraceHeader { Name = HeaderName }))
        {
            InnerHandler = sink,
        };
        using var client = new HttpClient(handler);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrivals = 0;

        async Task Send(string traceId)
        {
            using var scope = CorrelationContext.Begin(NullLogger.Instance, traceId);
            if (Interlocked.Increment(ref arrivals) == 2)
                ready.SetResult();
            await ready.Task.WaitAsync(TestContext.Current.CancellationToken);
            using var response = await client.GetAsync(
                $"https://example.test/{traceId}",
                TestContext.Current.CancellationToken
            );
        }

        await Task.WhenAll(
            Task.Run(() => Send("first"), TestContext.Current.CancellationToken),
            Task.Run(() => Send("second"), TestContext.Current.CancellationToken)
        );

        sink.Requests.Should().BeEquivalentTo(new[] { ("/first", "first"), ("/second", "second") });
        CorrelationContext.Current.Should().BeNull();
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public ConcurrentQueue<(string? Path, string? TraceId)> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Requests.Enqueue(
                (
                    request.RequestUri?.AbsolutePath,
                    request.Headers.TryGetValues(HeaderName, out var values) ? values.Single() : null
                )
            );

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
