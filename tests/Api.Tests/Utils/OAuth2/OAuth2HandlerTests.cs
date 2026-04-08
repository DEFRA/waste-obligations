using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Utils.OAuth2;
using Defra.WasteObligations.Testing;
using Defra.WasteObligations.Testing.Extensions.WireMock;
using Microsoft.Extensions.DependencyInjection;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Defra.WasteObligations.Api.Tests.Utils.OAuth2;

public class OAuth2HandlerTests(WireMockContext context) : WireMockTestBase(context)
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task WhenRequestsAreMade_ShouldGetTokenOnce(int requests)
    {
        const string accessToken = nameof(accessToken);
        WireMock.StubTokenRequest(accessToken);
        WireMock
            .Given(
                Request.Create().UsingGet().WithPath("/endpoint").WithHeader("Authorization", $"Bearer {accessToken}")
            )
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK));

        var services = CreateServices();
        await using var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(OAuth2HandlerTests));

        for (var i = 0; i < requests; i++)
        {
            var response = await client.GetAsync("/endpoint", TestContext.Current.CancellationToken);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        WireMock.LogEntries.Count(x => x.RequestMessage?.Path == "/token").Should().Be(1);
        WireMock.LogEntries.Count(x => x.RequestMessage?.Path == "/endpoint").Should().Be(requests);
    }

    [Fact]
    public async Task WhenConcurrentRequestsAreMade_ShouldGetTokenOnce()
    {
        const string accessToken = nameof(accessToken);
        WireMock.StubTokenRequest(accessToken);
        WireMock
            .Given(
                Request.Create().UsingGet().WithPath("/endpoint").WithHeader("Authorization", $"Bearer {accessToken}")
            )
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK));

        var services = CreateServices();
        await using var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(OAuth2HandlerTests));

        var request1 = client.GetAsync("/endpoint", TestContext.Current.CancellationToken);
        var request2 = client.GetAsync("/endpoint", TestContext.Current.CancellationToken);

        await Task.WhenAll(request1, request2);

        WireMock.LogEntries.Count(x => x.RequestMessage?.Path == "/token").Should().Be(1);
        WireMock.LogEntries.Count(x => x.RequestMessage?.Path == "/endpoint").Should().Be(2);
    }

    [Fact]
    public async Task WhenTokenExpires_ShouldGetTokenAgain()
    {
        const string accessToken = nameof(accessToken);
        WireMock.StubTokenRequest(accessToken, expiryInSeconds: 60);
        WireMock
            .Given(
                Request.Create().UsingGet().WithPath("/endpoint").WithHeader("Authorization", $"Bearer {accessToken}")
            )
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK));

        var services = CreateServices();
        await using var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(OAuth2HandlerTests));

        await client.GetAsync("/endpoint", TestContext.Current.CancellationToken);
        await client.GetAsync("/endpoint", TestContext.Current.CancellationToken);

        WireMock.LogEntries.Count(x => x.RequestMessage?.Path == "/token").Should().Be(2);
        WireMock.LogEntries.Count(x => x.RequestMessage?.Path == "/endpoint").Should().Be(2);
    }

    [Fact]
    public async Task WhenNoScope_ShouldNotSendScope()
    {
        const string accessToken = nameof(accessToken);
        WireMock.StubTokenRequest(accessToken, expiryInSeconds: 60, scope: null);
        WireMock
            .Given(
                Request.Create().UsingGet().WithPath("/endpoint").WithHeader("Authorization", $"Bearer {accessToken}")
            )
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK));

        var services = CreateServices(scope: null);
        await using var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(OAuth2HandlerTests));

        await client.GetAsync("/endpoint", TestContext.Current.CancellationToken);

        WireMock
            .LogEntries.Count(x =>
                x.RequestMessage?.Path == "/token" && x.RequestMessage?.Body?.Contains("scope") is false
            )
            .Should()
            .Be(1);
        WireMock.LogEntries.Count(x => x.RequestMessage?.Path == "/endpoint").Should().Be(1);
    }

    private ServiceCollection CreateServices(string? scope = "scope")
    {
        var result = new ServiceCollection();

        result
            .AddHttpClient(nameof(OAuth2HandlerTests))
            .AddHttpMessageHandler(sp => new OAuth2Handler(
                new OAuth2TokenCache(
                    sp.GetRequiredService<IHttpClientFactory>(),
                    new OAuth2Options
                    {
                        TokenEndpoint = Context.BaseAddress + "/token",
                        ClientId = "client_id",
                        ClientSecret = "client_secret",
                        Scope = scope,
                    }
                )
            ))
            .ConfigureHttpClient(httpClient => httpClient.BaseAddress = new Uri(Context.BaseAddress));

        return result;
    }
}
