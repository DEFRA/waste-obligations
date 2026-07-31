using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Utils.Health;
using Defra.WasteObligations.Api.Utils.Http;
using Defra.WasteObligations.Api.Utils.OAuth2;
using Defra.WasteObligations.Testing;
using Defra.WasteObligations.Testing.Extensions.WireMock;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Defra.WasteObligations.Api.Tests.Utils.Health;

public class OAuth2DownstreamHealthCheckTests(WireMockContext context) : WireMockTestBase(context)
{
    private const string Name = "downstream";
    private const string HealthEndpoint = "custom/health";

    [Fact]
    public async Task WhenAccessTokenAndDownstreamCallSucceed_ShouldReportBothStages()
    {
        const string accessToken = "access_token";
        WireMock.StubTokenRequest(accessToken);
        WireMock
            .Given(
                Request
                    .Create()
                    .UsingGet()
                    .WithPath($"/{HealthEndpoint}")
                    .WithHeader("Authorization", $"Bearer {accessToken}")
            )
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK));

        var result = await CheckHealth();
        var data = JsonSerializer.SerializeToElement(result.Data);

        result.Status.Should().Be(HealthStatus.Healthy);
        data.GetProperty("accessToken").GetProperty("status").GetString().Should().Be("Retrieved");
        data.GetProperty("downstream").GetProperty("status").GetString().Should().Be("Succeeded");
        data.GetProperty("downstream")
            .GetProperty("endpoint")
            .GetString()
            .Should()
            .Be($"{Context.BaseAddress}/{HealthEndpoint}");
    }

    [Fact]
    public async Task WhenDownstreamCallFails_ShouldReportRetrievedAccessTokenAndFailedDownstream()
    {
        const string accessToken = "access_token";
        WireMock.StubTokenRequest(accessToken);
        WireMock
            .Given(
                Request
                    .Create()
                    .UsingGet()
                    .WithPath($"/{HealthEndpoint}")
                    .WithHeader("Authorization", $"Bearer {accessToken}")
            )
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.ServiceUnavailable));

        var result = await CheckHealth();
        var data = JsonSerializer.SerializeToElement(result.Data);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Exception?.Message.Should().Be($"Failed to connect to {Name} after retrieving an access token");
        data.GetProperty("accessToken").GetProperty("status").GetString().Should().Be("Retrieved");
        data.GetProperty("downstream").GetProperty("status").GetString().Should().Be("Failed");
        data.GetProperty("downstream")
            .GetProperty("statusCode")
            .GetInt32()
            .Should()
            .Be((int)HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task WhenAccessTokenRetrievalFails_ShouldNotAttemptDownstreamCall()
    {
        var result = await CheckHealth();
        var data = JsonSerializer.SerializeToElement(result.Data);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Exception?.Message.Should().Be($"Failed to retrieve an access token for {Name}");
        data.GetProperty("accessToken").GetProperty("status").GetString().Should().Be("Failed");
        data.TryGetProperty("downstream", out _).Should().BeFalse();
        WireMock.LogEntries.Count(x => x.RequestMessage?.Path == $"/{HealthEndpoint}").Should().Be(0);
    }

    private async Task<HealthReportEntry> CheckHealth()
    {
        var services = new ServiceCollection();
        var configuration = new Dictionary<string, string?>
        {
            { $"{Name}:TokenEndpoint", $"{Context.BaseAddress}/token" },
            { $"{Name}:ClientId", "client_id" },
            { $"{Name}:ClientSecret", "client_secret" },
            { $"{Name}:Scope", "scope" },
        };

        services.AddLogging();
        services.AddOAuth2Client<OAuth2Options>(Name);
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(configuration).Build());
        services.AddTransient<ProxyHttpMessageHandler>();
        services
            .AddHealthChecks()
            .Add(
                new HealthCheckRegistration(
                    Name,
                    sp => new OAuth2DownstreamHealthCheck<OAuth2Options>(
                        sp,
                        Name,
                        HealthEndpoint,
                        (_, httpClient) => httpClient.BaseAddress = new Uri(Context.BaseAddress)
                    ),
                    HealthStatus.Unhealthy,
                    tags: []
                )
            );

        await using var serviceProvider = services.BuildServiceProvider();
        var report = await serviceProvider
            .GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(TestContext.Current.CancellationToken);

        return report.Entries[Name];
    }
}
