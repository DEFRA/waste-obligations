using AwesomeAssertions;
using Defra.WasteObligations.Api.Consumers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Defra.WasteObligations.Api.Tests.Consumers;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddConsumers_WhenAddingHostedConsumers_ShouldRegisterHostedConsumer()
    {
        var services = new ServiceCollection();

        services.AddConsumers(CreateConfiguration(), addHostedConsumers: true);

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

        services
            .Should()
            .NotContain(x =>
                x.ServiceType == typeof(IHostedService) && x.ImplementationType == typeof(AnalyticsAuditEventConsumer)
            );
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
