using AwesomeAssertions;
using Cronos;
using Defra.WasteObligations.Api.Data.Entities;
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

    [Theory]
    [InlineData(true, true, 2)]
    [InlineData(true, false, 1)]
    [InlineData(false, true, 1)]
    [InlineData(false, false, 0)]
    public void AddScheduledJobs_ShouldIndependentlyEnableSchedulerAndConventionProcessor(
        bool schedule,
        bool processing,
        int expectedWorkers
    )
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ObligationCalculation:Enabled"] = schedule ? "true" : "false",
                    ["ObligationCalculation:Cron"] = "0 1 * * *",
                    ["ObligationCalculation:Processing:Enabled"] = processing ? "true" : "false",
                }
            )
            .Build();
        var services = new ServiceCollection();

        services.AddScheduledJobs(configuration);

        services.Count(x => x.ServiceType == typeof(IHostedService)).Should().Be(expectedWorkers);
        services
            .Any(x =>
                x.ServiceType == typeof(IScheduledJobProcessor<ObligationCalculationInstruction>)
                && x.ImplementationType == typeof(ObligationCalculationProcessor)
            )
            .Should()
            .Be(processing);
        services
            .Any(x =>
                x.ServiceType == typeof(IScheduledJobWorkSource<ObligationCalculationInstruction>)
                && x.ImplementationType == typeof(ObligationCalculationWorkSource)
            )
            .Should()
            .Be(processing);
    }

    [Fact]
    public void AddScheduledJobs_WhenWorkersAreSuppressed_ShouldStillRegisterImplementationsWithoutStartingWork()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ObligationCalculation:Enabled"] = "true",
                    ["ObligationCalculation:Cron"] = "0 1 * * *",
                    ["ObligationCalculation:Processing:Enabled"] = "true",
                }
            )
            .Build();
        var services = new ServiceCollection();

        services.AddScheduledJobs(configuration, addWorkers: false);

        services.Should().NotContain(x => x.ServiceType == typeof(IHostedService));
        services.Should().Contain(x => x.ServiceType == typeof(ObligationCalculationJob));
        services
            .Should()
            .Contain(x => x.ServiceType == typeof(IScheduledJobProcessor<ObligationCalculationInstruction>));
    }

    [Fact]
    public void AddScheduledJobs_WhenEnabledProcessorIsMissing_ShouldRejectItsConventionName()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Missing:Processing:Enabled"] = "true" })
            .Build();
        var services = new ServiceCollection();

        var action = () => services.AddScheduledJobs(configuration);

        action.Should().Throw<InvalidOperationException>().WithMessage("*MissingProcessor*");
    }

    private static IConfiguration Configuration(string name, string? enabled, string? cron = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { [$"{name}:Enabled"] = enabled, [$"{name}:Cron"] = cron }
            )
            .Build();
}
