using System.Text;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Tests.Services.ObligationCalculations;

public class ObligationCalculationConfigurationTests(ApiWebApplicationFactory factory)
    : IClassFixture<ApiWebApplicationFactory>
{
    [Theory]
    [InlineData(2025, .55, .75, .75, .61, .80, .45, .74, .75)]
    [InlineData(2026, .57, .77, .77, .62, .81, .46, .76, .76)]
    [InlineData(2027, .59, .79, .79, .63, .82, .47, .78, .77)]
    [InlineData(2028, .61, .81, .81, .64, .83, .48, .80, .78)]
    [InlineData(2029, .63, .83, .83, .65, .84, .49, .82, .79)]
    [InlineData(2030, .65, .85, .85, .67, .85, .50, .85, .80)]
    public void ApplicationConfiguration_ShouldSupplyLegacySeeds(int year, params double[] expected)
    {
        var targets = factory.Services.GetRequiredService<IOptions<ObligationCalculationOptions>>().Value.Targets;

        targets.Keys.Should().BeEquivalentTo(2025, 2026, 2027, 2028, 2029, 2030);
        targets[year]
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, double>
                {
                    ["PL"] = expected[0],
                    ["PC"] = expected[1],
                    ["FC"] = expected[2],
                    ["AL"] = expected[3],
                    ["ST"] = expected[4],
                    ["WD"] = expected[5],
                    ["GL"] = expected[6],
                    ["GR"] = expected[7],
                }
            );
        factory.Services.GetRequiredService<IObligationCalculator>().Should().BeOfType<ObligationCalculator>();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("incomplete")]
    [InlineData("unsupported")]
    [InlineData("negative")]
    [InlineData("above-one")]
    [InlineData("non-finite")]
    [InlineData("case-variant")]
    [InlineData("invalid-year")]
    [InlineData("duplicate-year")]
    public async Task InvalidConfiguration_ShouldPreventStartup(string invalid)
    {
        var values = ValidConfiguration();
        switch (invalid)
        {
            case "missing":
                values.Clear();
                break;
            case "incomplete":
                values.Remove("ObligationCalculation:Targets:2025:GR");
                break;
            case "unsupported":
                values["ObligationCalculation:Targets:2025:XX"] = ".5";
                break;
            case "negative":
                values["ObligationCalculation:Targets:2025:PL"] = "-.1";
                break;
            case "above-one":
                values["ObligationCalculation:Targets:2025:PL"] = "1.1";
                break;
            case "non-finite":
                values["ObligationCalculation:Targets:2025:PL"] = "NaN";
                break;
            case "case-variant":
                values.Remove("ObligationCalculation:Targets:2025:PL");
                values["ObligationCalculation:Targets:2025:pl"] = ".55";
                break;
            case "invalid-year":
                values["ObligationCalculation:Targets:not-a-year:PL"] = ".55";
                break;
            case "duplicate-year":
                values["ObligationCalculation:Targets:02025:PL"] = ".55";
                break;
        }
        using var host = CreateHost(values);

        var act = () => host.StartAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<OptionsValidationException>();
    }

    [Fact]
    public async Task CompleteConfiguration_ShouldAllowStartup()
    {
        using var host = CreateHost(ValidConfiguration());

        await host.StartAsync(TestContext.Current.CancellationToken);

        host.Services.GetRequiredService<IObligationCalculator>().Should().BeOfType<ObligationCalculator>();
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("PL")]
    [InlineData("pl")]
    public void DuplicateMaterialConfiguration_ShouldBeRejectedByConfigurationProvider(string duplicate)
    {
        using var json = new MemoryStream(
            Encoding.UTF8.GetBytes(
                "{\"ObligationCalculation\":{\"Targets\":{\"2025\":{\"PL\":0.55,\"" + duplicate + "\":0.55}}}}"
            )
        );

        var act = () => new ConfigurationBuilder().AddJsonStream(json).Build();

        act.Should().Throw<FormatException>();
    }

    private static IHost CreateHost(Dictionary<string, string?> values) =>
        new HostBuilder()
            .ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(values))
            .ConfigureServices(services => services.AddObligationCalculation())
            .Build();

    private static Dictionary<string, string?> ValidConfiguration() =>
        new()
        {
            ["ObligationCalculation:Targets:2025:PL"] = ".55",
            ["ObligationCalculation:Targets:2025:PC"] = ".75",
            ["ObligationCalculation:Targets:2025:FC"] = ".75",
            ["ObligationCalculation:Targets:2025:AL"] = ".61",
            ["ObligationCalculation:Targets:2025:ST"] = ".80",
            ["ObligationCalculation:Targets:2025:WD"] = ".45",
            ["ObligationCalculation:Targets:2025:GL"] = ".74",
            ["ObligationCalculation:Targets:2025:GR"] = ".75",
        };
}
