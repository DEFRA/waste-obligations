using AwesomeAssertions;
using Defra.WasteObligations.AuditEvents;
using Defra.WasteObligations.AuditEvents.Analytics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Tests.AuditEvents.Analytics;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAuditEvents_WhenAddingAnalyticsProcessor_ShouldRegisterHostedProcessor()
    {
        var services = new ServiceCollection();

        services.AddAuditEvents(CreateConfiguration(), addAnalyticsProcessor: true);
        services.AddAuditEventQueueDispatcher(CreateConfiguration(), "queue", "QueueDispatcher", true);

        services.Count(x => x.ServiceType == typeof(IHostedService)).Should().Be(2);

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
        services.Should().Contain(x => x.ServiceType == typeof(SnsAnalyticsEventSender));
        services
            .Should()
            .Contain(x => x.ServiceType == typeof(IAnalyticsEventSender) && x.ImplementationFactory != null);
    }

    [Theory]
    [InlineData("")]
    [InlineData("obligation_calculations")]
    public void AddAuditEvents_WhenEnabledWithMissingOrUnknownEntity_ShouldRejectConfiguration(string entity)
    {
        var values = new Dictionary<string, string?>
        {
            ["AnalyticsAuditEventProcessor:ProcessName"] = "analytics",
            ["AnalyticsAuditEventProcessor:TopicArn"] = "arn:aws:sns:eu-west-2:000000000000:events",
            ["AnalyticsAuditEventProcessor:ProcessingEnabled"] = "true",
        };
        if (entity.Length > 0)
            values["AnalyticsAuditEventProcessor:IncludedEntities:0"] = entity;
        var services = new ServiceCollection();
        services.AddAuditEvents(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), false);
        using var provider = services.BuildServiceProvider();

        var read = () => provider.GetRequiredService<IOptions<AnalyticsAuditEventProcessorOptions>>().Value;

        read.Should().Throw<OptionsValidationException>();
    }

    [Theory]
    [InlineData("", "calculation-queue")]
    [InlineData("https://example.test/queue", "analytics")]
    public void AddAuditEventQueueDispatcher_WhenDestinationOrProcessIsInvalid_ShouldRejectConfiguration(
        string queueUrl,
        string processName
    )
    {
        var values = new Dictionary<string, string?>
        {
            ["AnalyticsAuditEventProcessor:ProcessName"] = "analytics",
            ["QueueDispatcher:ProcessName"] = processName,
            ["QueueDispatcher:QueueUrl"] = queueUrl,
            ["QueueDispatcher:ProcessingEnabled"] = "true",
            ["QueueDispatcher:IncludedEntities:0"] = "obligation_calculation",
        };
        var services = new ServiceCollection();
        services.AddAuditEventQueueDispatcher(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            "queue",
            "QueueDispatcher",
            false
        );
        using var provider = services.BuildServiceProvider();

        var read = () =>
            provider.GetRequiredService<IOptionsMonitor<AnalyticsAuditEventProcessorOptions>>().Get("queue");

        read.Should().Throw<OptionsValidationException>();
    }

    private static IConfiguration CreateConfiguration()
    {
        var values = new Dictionary<string, string?>
        {
            [$"{AnalyticsAuditEventProcessorOptions.SectionName}:ProcessName"] = "analytics",
            [$"{AnalyticsAuditEventProcessorOptions.SectionName}:TopicArn"] =
                "arn:aws:sns:eu-west-2:000000000000:waste_obligations_analytics_events",
            [$"{AnalyticsAuditEventProcessorOptions.SectionName}:ProcessingEnabled"] = "true",
            [$"{AnalyticsAuditEventProcessorOptions.SectionName}:BatchSize"] = "25",
            [$"{AnalyticsAuditEventProcessorOptions.SectionName}:PollIntervalSeconds"] = "15",
            [$"{AnalyticsAuditEventProcessorOptions.SectionName}:PollJitterSeconds"] = "5",
            [$"{AnalyticsAuditEventProcessorOptions.SectionName}:LeaseDurationSeconds"] = "60",
        };

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
