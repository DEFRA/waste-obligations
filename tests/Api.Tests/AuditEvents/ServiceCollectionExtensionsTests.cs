using AwesomeAssertions;
using Defra.WasteObligations.AuditEvents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Defra.WasteObligations.Api.Tests.AuditEvents;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAuditEvents_WhenAddingAnalyticsProcessor_ShouldRegisterHostedProcessor()
    {
        var services = new ServiceCollection();

        services.AddAuditEvents(CreateConfiguration(), addAnalyticsProcessor: true);

        services
            .Should()
            .Contain(x =>
                x.ServiceType == typeof(IHostedService) && x.ImplementationType == typeof(AnalyticsAuditEventProcessor)
            );
    }

    [Fact]
    public void AddAuditEvents_WhenNotAddingAnalyticsProcessor_ShouldNotRegisterHostedProcessor()
    {
        var services = new ServiceCollection();

        services.AddAuditEvents(CreateConfiguration(), addAnalyticsProcessor: false);

        services
            .Should()
            .NotContain(x =>
                x.ServiceType == typeof(IHostedService) && x.ImplementationType == typeof(AnalyticsAuditEventProcessor)
            );
    }

    [Fact]
    public void AddAuditEvents_ShouldRegisterCoreServices()
    {
        var services = new ServiceCollection();

        services.AddAuditEvents(CreateConfiguration());

        services.Should().Contain(x => x.ServiceType == typeof(IAuditEventService));
        services.Should().Contain(x => x.ServiceType == typeof(AuditEventLeaseService));
        services.Should().Contain(x => x.ServiceType == typeof(AuditEventDispatchService));
        services.Should().Contain(x => x.ServiceType == typeof(IAnalyticsEventSender));
    }

    private static IConfiguration CreateConfiguration()
    {
        var values = new Dictionary<string, string?>
        {
            [$"{AnalyticsAuditEventProcessorOptions.SectionName}:ProcessName"] = "analytics",
            [$"{AnalyticsAuditEventProcessorOptions.SectionName}:BatchSize"] = "25",
            [$"{AnalyticsAuditEventProcessorOptions.SectionName}:PollIntervalSeconds"] = "15",
            [$"{AnalyticsAuditEventProcessorOptions.SectionName}:PollJitterSeconds"] = "5",
            [$"{AnalyticsAuditEventProcessorOptions.SectionName}:LeaseDurationSeconds"] = "60",
        };

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
