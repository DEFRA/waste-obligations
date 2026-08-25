using System.Diagnostics;
using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Utils.Http;
using Defra.WasteObligations.Api.Utils.OAuth2;
using Defra.WasteObligations.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly.Timeout;
using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Defra.WasteObligations.Api.Tests.Utils.OAuth2;

public sealed class OAuth2ClientResilienceTests : IDisposable
{
    private WireMockContext Context { get; } = new();
    private WireMockServer WireMock => Context.Server;

    [Fact]
    public async Task WhenTokenRequestExceedsAttemptTimeout_ShouldRetryUpToTheConfiguredBudget()
    {
        WireMock
            .Given(Request.Create().UsingPost().WithPath("/token"))
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithBodyAsJson(new { access_token = "access_token", expires_in = 3600 })
                    .WithDelay(TimeSpan.FromMilliseconds(250))
            );
        await using var serviceProvider = CreateServices(attemptTimeout: "00:00:00.05").BuildServiceProvider();
        var subject = new OAuth2Client(serviceProvider.GetRequiredService<IHttpClientFactory>());

        var act = () => subject.RequestTokenAsync(CreateOptions(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<TimeoutRejectedException>();
    }

    [Fact]
    public async Task WhenTokenEndpointRecoversFromTransientFailure_ShouldRetryAndReturnSuccess()
    {
        var handler = new RecoveringTokenEndpointHandler();
        await using var serviceProvider = CreateServices(handler: handler).BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(OAuth2Client.HttpClientName);
        using var request = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
        ]);

        using var response = await client.PostAsync(
            $"{Context.BaseAddress}/token",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.RequestCount.Should().Be(2);
    }

    [Fact]
    public async Task WhenTokenEndpointRemainsUnavailable_ShouldStopAfterTheConfiguredRetryBudget()
    {
        WireMock
            .Given(Request.Create().UsingPost().WithPath("/token"))
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.ServiceUnavailable));
        await using var serviceProvider = CreateServices().BuildServiceProvider();
        var subject = new OAuth2Client(serviceProvider.GetRequiredService<IHttpClientFactory>());

        var act = () => subject.RequestTokenAsync(CreateOptions(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<HttpRequestException>();
        WireMock.LogEntries.Count(x => x.RequestMessage?.Path == "/token").Should().Be(3);
    }

    [Fact]
    public async Task WhenMultipleDownstreamsUseTheTokenClient_ShouldAddTheResiliencePipelineOnce()
    {
        WireMock
            .Given(Request.Create().UsingPost().WithPath("/token"))
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.ServiceUnavailable));
        var services = CreateServices();
        services.AddOAuth2Client<OAuth2Options>("SecondOAuth2");
        await using var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(OAuth2Client.HttpClientName);
        using var request = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
        ]);

        using var response = await client.PostAsync(
            $"{Context.BaseAddress}/token",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        WireMock.LogEntries.Count(x => x.RequestMessage?.Path == "/token").Should().Be(3);
    }

    [Fact]
    public async Task WhenTokenRequestExceedsTotalTimeout_ShouldCancelTheInFlightRequest()
    {
        WireMock
            .Given(Request.Create().UsingPost().WithPath("/token"))
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithBodyAsJson(new { access_token = "access_token", expires_in = 3600 })
                    .WithDelay(TimeSpan.FromMilliseconds(250))
            );
        await using var serviceProvider = CreateServices(totalRequestTimeout: "00:00:00.05", attemptTimeout: "00:00:01")
            .BuildServiceProvider();
        var subject = new OAuth2Client(serviceProvider.GetRequiredService<IHttpClientFactory>());

        var act = () => subject.RequestTokenAsync(CreateOptions(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<TimeoutRejectedException>();
    }

    [Fact]
    public async Task WhenCallerCancelsTokenRequest_ShouldCancelPromptlyWithoutWaitingForTheAttemptTimeout()
    {
        WireMock
            .Given(Request.Create().UsingPost().WithPath("/token"))
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithBodyAsJson(new { access_token = "access_token", expires_in = 3600 })
                    .WithDelay(TimeSpan.FromSeconds(2))
            );
        await using var serviceProvider = CreateServices(totalRequestTimeout: "00:00:05", attemptTimeout: "00:00:04")
            .BuildServiceProvider();
        var subject = new OAuth2Client(serviceProvider.GetRequiredService<IHttpClientFactory>());
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var stopwatch = Stopwatch.StartNew();

        var act = () => subject.RequestTokenAsync(CreateOptions(), cancellationTokenSource.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    private static ServiceCollection CreateServices(
        string totalRequestTimeout = "00:00:10",
        string attemptTimeout = "00:00:03",
        DelegatingHandler? handler = null
    )
    {
        var configuration = new Dictionary<string, string?>
        {
            { $"{OAuth2Client.HttpClientName}:TotalRequestTimeout:Timeout", totalRequestTimeout },
            { $"{OAuth2Client.HttpClientName}:Retry:MaxRetryAttempts", "2" },
            { $"{OAuth2Client.HttpClientName}:Retry:Delay", "00:00:00.01" },
            { $"{OAuth2Client.HttpClientName}:Retry:BackoffType", "Constant" },
            { $"{OAuth2Client.HttpClientName}:Retry:UseJitter", "false" },
            { $"{OAuth2Client.HttpClientName}:AttemptTimeout:Timeout", attemptTimeout },
        };
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(configuration).Build());
        services.AddTransient<ProxyHttpMessageHandler>();
        services.AddOAuth2Client<OAuth2Options>("OAuth2");

        if (handler is not null)
        {
            services.AddHttpClient(OAuth2Client.HttpClientName).AddHttpMessageHandler(() => handler);
        }

        return services;
    }

    private OAuth2Options CreateOptions() =>
        new()
        {
            TokenEndpoint = $"{Context.BaseAddress}/token",
            ClientId = "client_id",
            ClientSecret = "client_secret",
            Scope = "scope",
        };

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Context.Dispose();
    }

    private sealed class RecoveringTokenEndpointHandler : DelegatingHandler
    {
        private int _requestCount;

        public int RequestCount => _requestCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var statusCode =
                Interlocked.Increment(ref _requestCount) == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK;

            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }
}
