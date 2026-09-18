using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace Defra.WasteObligations.HealthCheck.Tests;

public sealed class HealthCheckTests
{
    [Theory]
    [InlineData(200, "Healthy", true)]
    [InlineData(200, "Degraded", false)]
    [InlineData(503, "Unhealthy", false)]
    [InlineData(503, "Healthy", false)]
    [InlineData(401, "Healthy", false)]
    [InlineData(302, "Healthy", false)]
    [InlineData(200, "Unexpected", false)]
    public async Task ChecksHttpAndReportStatus(int statusCode, string status, bool healthy)
    {
        using var handler = new StubHandler(
            (_, _) => Task.FromResult(Response(statusCode, JsonSerializer.Serialize(new { status })))
        );
        using var client = new HttpClient(handler);
        var result = await new HealthCheckRunner(client).Check("service", Service(), 10, CancellationToken.None);
        result.Healthy.Should().Be(healthy);
        result.HttpStatus.Should().Be(statusCode);
    }

    [Theory]
    [InlineData("<html>Sign in</html>")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"status\":1}")]
    [InlineData("{\"status\":\"Healthy\",\"results\":[]}")]
    public async Task InvalidReportsNeverPass(string body)
    {
        using var handler = new StubHandler((_, _) => Task.FromResult(Response(200, body)));
        using var client = new HttpClient(handler);
        var result = await new HealthCheckRunner(client).Check("service", Service(), 10, CancellationToken.None);
        result.Healthy.Should().BeFalse();
    }

    [Fact]
    public async Task ReportsNestedDependencyFailures()
    {
        const string body = """
            {"status":"Healthy","results":{"Gateway":{"status":"Healthy","response":{"status":"Unhealthy","results":{"Database":{"status":"Unhealthy"}}}}}}
            """;
        using var handler = new StubHandler((_, _) => Task.FromResult(Response(200, body)));
        using var client = new HttpClient(handler);
        var result = await new HealthCheckRunner(client).Check("service", Service(), 10, CancellationToken.None);
        result.Healthy.Should().BeFalse();
        result.Checks["Gateway/Database"].Should().Be("Unhealthy");
    }

    [Fact]
    public async Task SendsOAuthFormAndApiKeyOnlyToTheirDestinations()
    {
        const string clientId = "client&identifier";
        const string testValue = "test";
        var calls = 0;
        using var handler = new StubHandler(
            async (request, token) =>
            {
                calls++;
                if (request.RequestUri!.AbsolutePath == "/token")
                {
                    request.Method.Should().Be(HttpMethod.Post);
                    request.Headers.Contains("X-Health-Check-Token").Should().BeFalse();
                    request.Headers.Authorization.Should().BeNull();
                    request.Content.Should().NotBeNull();
                    (await request.Content.ReadAsStringAsync(token))
                        .Should()
                        .Be("grant_type=client_credentials&client_id=client%26identifier&client_secret=test");

                    return Response(200, "{\"access_token\":\"access-token\"}");
                }
                request.Method.Should().Be(HttpMethod.Get);
                request.RequestUri.AbsolutePath.Should().Be("/health/all");
                if (calls == 3)
                {
                    request.Headers.Authorization.Should().BeNull();
                    request.Headers.Contains("X-Health-Check-Token").Should().BeFalse();

                    return Response(200, "{\"status\":\"Healthy\"}");
                }
                request.Headers.Authorization!.Scheme.Should().Be("Bearer");
                request.Headers.Authorization.Parameter.Should().Be("access-token");
                request.Headers.GetValues("X-Health-Check-Token").Should().ContainSingle().Which.Should().Be(testValue);

                return Response(200, "{\"status\":\"Healthy\"}");
            }
        );
        using var client = new HttpClient(handler);
        var service = new ServiceConfiguration
        {
            Url = "https://service.test/health/all",
            ApiKey = new ApiKeyConfiguration { Value = testValue },
            OAuth = new OAuthConfiguration
            {
                TokenUrl = "https://identity.test/token",
                ClientId = clientId,
                ClientSecret = testValue,
            },
        };
        (await new HealthCheckRunner(client).Check("service", service, 10, CancellationToken.None))
            .Healthy.Should()
            .BeTrue();
        (await new HealthCheckRunner(client).Check("service", Service(), 10, CancellationToken.None))
            .Healthy.Should()
            .BeTrue();
        calls.Should().Be(3);
    }

    [Theory]
    [InlineData(401, "{\"error\":\"secret\"}")]
    [InlineData(200, "{}")]
    [InlineData(200, "{\"access_token\":null}")]
    [InlineData(200, "{\"access_token\":\"bad token\"}")]
    public async Task OAuthFailuresDoNotCallHealthOrExposeResponse(int statusCode, string body)
    {
        const string credential = "secret";
        var calls = 0;
        using var handler = new StubHandler(
            (_, _) =>
            {
                calls++;

                return Task.FromResult(Response(statusCode, body));
            }
        );
        using var client = new HttpClient(handler);
        var result = await new HealthCheckRunner(client).Check(
            "service",
            new ServiceConfiguration
            {
                Url = "https://service.test/health/all",
                OAuth = new OAuthConfiguration
                {
                    TokenUrl = "https://identity.test/token",
                    ClientId = credential,
                    ClientSecret = credential,
                },
            },
            10,
            CancellationToken.None
        );
        calls.Should().Be(1);
        result.Healthy.Should().BeFalse();
        result.Status.Should().StartWith("OAuth").And.NotContain("secret");
    }

    [Fact]
    public async Task TimeoutIsAReportedFailure()
    {
        using var handler = new StubHandler(
            async (_, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);

                return Response(200, "{}");
            }
        );
        using var client = new HttpClient(handler);
        var result = await new HealthCheckRunner(client).Check("service", Service(), 1, CancellationToken.None);
        result.Healthy.Should().BeFalse();
        result.Status.Should().Be("Unhealthy (Health request timed out after 1s)");
    }

    [Fact]
    public async Task CommandContinuesAfterNetworkFailureAndReturnsJson()
    {
        using var config = new ConfigurationFile(
            """
            {"Services":{"broken":{"Url":"https://broken.test/health/all"},"working":{"Url":"https://working.test/health/all"}}}
            """
        );
        using var handler = new StubHandler(
            (request, _) =>
                request.RequestUri!.Host == "broken.test"
                    ? throw new HttpRequestException("sensitive response")
                    : Task.FromResult(Response(200, "{\"status\":\"Healthy\"}"))
        );
        using var client = new HttpClient(handler);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = await HealthCheckCommand.Run([config.Path, "--json"], client, output, error, CancellationToken.None);
        code.Should().Be(1);
        using var json = JsonDocument.Parse(output.ToString());
        json.RootElement.GetArrayLength().Should().Be(2);
        json.RootElement[1].GetProperty("healthy").GetBoolean().Should().BeTrue();
        json.RootElement[0].GetProperty("name").GetString().Should().Be("broken");
        json.RootElement[1].GetProperty("name").GetString().Should().Be("working");
        output.ToString().Should().NotContain("sensitive response");
        error
            .ToString()
            .Should()
            .Contain("Checking broken: GET https://broken.test/health/all")
            .And.Contain("Checking working: GET https://working.test/health/all");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReportsProgressBeforeWaitingAndContinuesAfterTimeout(bool jsonOutput)
    {
        using var config = new ConfigurationFile(
            """
            {"TimeoutSeconds":1,"Services":{
              "a-slow":{"Url":"https://slow.test/health/all?key=hidden"},
              "b-healthy":{"Url":"https://healthy.test/health/all"}
            }}
            """
        );
        using var output = new StringWriter();
        using var error = new StringWriter();
        using var handler = new StubHandler(
            async (request, token) =>
            {
                var progress = jsonOutput ? error : output;
                if (request.RequestUri!.Host == "slow.test")
                {
                    progress
                        .ToString()
                        .Should()
                        .Contain("Checking a-slow: GET https://slow.test/health/all (timeout 1s)")
                        .And.NotContain("hidden");
                    await Task.Delay(Timeout.Infinite, token);
                }
                progress.ToString().Should().Contain("Checking b-healthy: GET https://healthy.test/health/all");

                return Response(200, "{\"status\":\"Healthy\"}");
            }
        );
        using var client = new HttpClient(handler);
        string[] args = jsonOutput ? [config.Path, "--json"] : [config.Path];
        (await HealthCheckCommand.Run(args, client, output, error, CancellationToken.None)).Should().Be(1);
        output.ToString().Should().Contain("Unhealthy (Health request timed out after 1s)");
        if (jsonOutput)
        {
            using var json = JsonDocument.Parse(output.ToString());
            json.RootElement[0].GetProperty("healthy").GetBoolean().Should().BeFalse();
            json.RootElement[1].GetProperty("healthy").GetBoolean().Should().BeTrue();
        }
        else
        {
            output.ToString().Should().Contain("FAIL a-slow").And.Contain("PASS b-healthy");
            error.ToString().Should().BeEmpty();
        }
    }

    [Theory]
    [InlineData("{\"Services\":{}}")]
    [InlineData("{\"Services\":null}")]
    [InlineData("{\"Services\":{\"missing\":null}}")]
    [InlineData("{\"TimeoutSeconds\":0,\"Services\":{\"a\":{\"Url\":\"https://test/health/all\"}}}")]
    [InlineData("{\"TimeoutSeconds\":6,\"Services\":{\"a\":{\"Url\":\"https://test/health/all\"}}}")]
    [InlineData("{\"Services\":{\"a\":{\"Url\":\"http://remote.test/health/all\"}}}")]
    [InlineData("{\"Services\":{\"a\":{\"Url\":\"https://test/health/all\",\"OAuth\":{\"TokenUrl\":\"invalid\"}}}}")]
    [InlineData("{\"Services\":{\"a\":{\"Url\":\"https://test\"},\"A\":{\"Url\":\"https://test\"}}}")]
    [InlineData(
        "{\"Services\":{\"a\":{\"Url\":\"https://test\",\"ApiKey\":{\"Value\":\"\",\"HeaderName\":\"X-Health-Check-Token\"}}}}"
    )]
    [InlineData("{\"Services\":{\"\":{\"Url\":\"https://test\"}}}")]
    public async Task InvalidConfigurationFailsBeforeRequests(string json)
    {
        using var config = new ConfigurationFile(json);
        using var handler = new StubHandler((_, _) => throw new Xunit.Sdk.XunitException("No network call expected"));
        using var client = new HttpClient(handler);
        using var output = new StringWriter();
        using var error = new StringWriter();
        (await HealthCheckCommand.Run([config.Path], client, output, error, CancellationToken.None)).Should().Be(2);
        output.ToString().Should().BeEmpty();
        error.ToString().Should().Contain("Invalid configuration");
    }

    private static ServiceConfiguration Service() => new() { Url = "https://service.test/health/all" };

    private static HttpResponseMessage Response(int code, string body) =>
        new((HttpStatusCode)code) { Content = new StringContent(body) };

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => send(request, cancellationToken);
    }

    private sealed class ConfigurationFile : IDisposable
    {
        public string Path { get; } = System.IO.Path.GetTempFileName();

        public ConfigurationFile(string content) => File.WriteAllText(Path, content);

        public void Dispose() => File.Delete(Path);
    }
}
