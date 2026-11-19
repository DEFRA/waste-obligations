using AwesomeAssertions;
using Defra.WasteObligations.Api.Consumers;
using Defra.WasteObligations.AuditEvents;
using Defra.WasteObligations.AuditEvents.Analytics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Tests.Consumers;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddConsumers_WhenAddingHostedConsumers_ShouldRegisterHostedConsumer()
    {
        var services = new ServiceCollection();

        services.AddConsumers(CreateConfiguration(), addHostedConsumers: true);

        services.Count(x => x.ServiceType == typeof(IHostedService)).Should().Be(1);

        services
            .Should()
            .Contain(x =>
                x.ServiceType == typeof(IHostedService) && x.ImplementationType == typeof(AnalyticsAuditEventConsumer)
            );
    }

    [Fact]
    public void AddConsumers_WhenNotAddingHostedConsumers_ShouldNotRegisterHostedConsumer()
    {
        var services = new ServiceCollection();

        services.AddConsumers(CreateConfiguration(), addHostedConsumers: false);

        services.Should().NotContain(x => x.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddAuditEventsAndConsumers_ShouldAllowBothDispatchersToUseOneSubscribedQueue()
    {
        const string queueUrl = "http://localhost:4566/000000000000/waste_obligations_analytics_events_queue";
        const string topicArn = "arn:aws:sns:eu-west-2:000000000000:events";
        var configuration = CreateConfiguration();
        configuration["AnalyticsAuditEventProcessor:ProcessName"] = "analytics";
        configuration["AnalyticsAuditEventProcessor:TopicArn"] = topicArn;
        configuration["AnalyticsAuditEventProcessor:ProcessingEnabled"] = "true";
        configuration["AnalyticsAuditEventProcessor:IncludedEntities:0"] = "compliance_declaration";
        configuration["CalculationAuditEventProcessor:ProcessName"] = "calculation";
        configuration["CalculationAuditEventProcessor:QueueUrl"] = queueUrl;
        configuration["CalculationAuditEventProcessor:ProcessingEnabled"] = "true";
        configuration["CalculationAuditEventProcessor:IncludedEntities:0"] = "obligation_calculation";
        configuration["AnalyticsAuditEventConsumer:ProcessingEnabled"] = "true";
        var services = new ServiceCollection();
        services.AddAuditEvents(configuration, false);
        services.AddConsumers(configuration, false);
        using var provider = services.BuildServiceProvider();

        var dispatchers = provider.GetRequiredService<IOptionsMonitor<AnalyticsAuditEventProcessorOptions>>();
        var consumer = provider.GetRequiredService<IOptions<AnalyticsAuditEventConsumerOptions>>().Value;

        dispatchers.CurrentValue.TopicArn.Should().Be(topicArn);
        dispatchers.Get(AnalyticsAuditEventProcessorOptions.CalculationSectionName).QueueUrl.Should().Be(queueUrl);
        consumer.QueueUrl.Should().Be(queueUrl);
        consumer.ProcessingEnabled.Should().BeTrue();
    }

    private static IConfiguration CreateConfiguration()
    {
        var values = new Dictionary<string, string?>
        {
            [$"{AnalyticsAuditEventConsumerOptions.SectionName}:QueueUrl"] =
                "http://localhost:4566/000000000000/waste_obligations_analytics_events_queue",
            [$"{AnalyticsAuditEventConsumerOptions.SectionName}:ProcessingEnabled"] = "false",
            [$"{AnalyticsAuditEventConsumerOptions.SectionName}:BatchSize"] = "10",
            [$"{AnalyticsAuditEventConsumerOptions.SectionName}:WaitTimeSeconds"] = "20",
            [$"{AnalyticsAuditEventConsumerOptions.SectionName}:PollIntervalSeconds"] = "15",
        };

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
