using System.Net;
using AwesomeAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Defra.WasteObligations.Api.Tests.Endpoints.Health;

public sealed class EmailDeliveryCutoverHealthTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    [Theory]
    [InlineData(false, null)]
    [InlineData(true, "2027-01-01T00:00:00.1234567+00:00")]
    public async Task WhenExtendedHealthRequested_ShouldReportEffectiveCutover(bool configured, string? cutover)
    {
        using var configuredFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration(configuration =>
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?> { ["EmailDelivery:EmailDeliveryCutoverUtc"] = cutover }
                )
            );
            builder.ConfigureTestServices(services =>
                services.Configure<HealthCheckServiceOptions>(options =>
                {
                    foreach (
                        var check in options
                            .Registrations.Where(check => check.Name != "EmailDeliveryCutover")
                            .ToArray()
                    )
                        options.Registrations.Remove(check);
                })
            );
        });
        using var client = configuredFactory.CreateClient();
        using var response = await client.GetAsync("/health/all", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .UseParameters(configured)
            .DontScrubDateTimes();
        using var ready = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        ready.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ready.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("Healthy");
    }
}
