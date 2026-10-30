using AwesomeAssertions;
using Cronos;
using Defra.WasteObligations.Api.ScheduledJobs;
using Defra.WasteObligations.Api.Services.Scheduling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Defra.WasteObligations.Api.Tests.Services.Scheduling;

public class ServiceCollectionExtensionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public void AddScheduledJobs_WhenNotEnabled_ShouldNotRegisterWorker(string? enabled)
    {
        var configuration = Configuration("NotImplemented", enabled);
        var services = new ServiceCollection();

        services.AddScheduledJobs(configuration);

        services.Should().NotContain(x => x.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddScheduledJobs_WhenEnabled_ShouldResolveImplementationByConvention()
    {
        var configuration = Configuration("ObligationCalculation", "true", "0 1 * * *");
        var services = new ServiceCollection();

        services.AddScheduledJobs(configuration);

        services.Should().ContainSingle(x => x.ServiceType == typeof(IHostedService));
        services
            .Should()
            .ContainSingle(x =>
                x.ServiceType == typeof(ObligationCalculationJob)
                && x.ImplementationType == typeof(ObligationCalculationJob)
                && x.Lifetime == ServiceLifetime.Scoped
            );
    }

    [Fact]
    public void AddScheduledJobs_WhenEnabledWithoutMatchingImplementation_ShouldRejectConfiguration()
    {
        var services = new ServiceCollection();

        var action = () => services.AddScheduledJobs(Configuration("NotImplemented", "true", "0 1 * * *"));

        action.Should().Throw<InvalidOperationException>().WithMessage("*NotImplementedJob*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    public void AddScheduledJobs_WhenEnabledWithoutValidCron_ShouldRejectConfiguration(string? cron)
    {
        var services = new ServiceCollection();

        var action = () => services.AddScheduledJobs(Configuration("ObligationCalculation", "true", cron));

        if (cron is null)
            action.Should().Throw<InvalidOperationException>();
        else
            action.Should().Throw<CronFormatException>();
    }

    private static IConfiguration Configuration(string name, string? enabled, string? cron = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { [$"{name}:Enabled"] = enabled, [$"{name}:Cron"] = cron }
            )
            .Build();
}
