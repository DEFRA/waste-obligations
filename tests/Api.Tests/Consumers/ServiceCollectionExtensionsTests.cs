using AwesomeAssertions;
using Defra.WasteObligations.Api.Consumers;
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

        services.Count(x => x.ServiceType == typeof(IHostedService)).Should().Be(2);

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

    [Theory]
    [InlineData("https://sqs.eu-west-2.amazonaws.com/123/calculation")]
    [InlineData("https://sqs.eu-west-2.amazonaws.com/123/calculation/")]
    [InlineData(null)]
    [InlineData("set-automatically-when-deployed")]
    [InlineData("ftp://example.test/calculation")]
    public void AddConsumers_WhenLoggingConsumerSharesDestinationOrDestinationIsUnknown_ShouldRejectConfiguration(
        string? destination
    )
    {
        var configuration = CreateConfiguration();
        configuration["CalculationAuditEventProcessor:QueueUrl"] = destination;
        configuration["CalculationAuditEventConsumer:QueueUrl"] = "https://sqs.eu-west-2.amazonaws.com/123/calculation";
        configuration["CalculationAuditEventConsumer:ProcessingEnabled"] = "true";
        var services = new ServiceCollection();
        services.AddConsumers(configuration, false);
        using var provider = services.BuildServiceProvider();

        var read = () =>
            provider
                .GetRequiredService<IOptionsMonitor<AnalyticsAuditEventConsumerOptions>>()
                .Get(AnalyticsAuditEventConsumerOptions.CalculationSectionName);

        read.Should().Throw<OptionsValidationException>().WithMessage("*isolated diagnostic queue*");
    }

    [Theory]
    [InlineData(true, "https://sqs.eu-west-2.amazonaws.com/123/diagnostic")]
    [InlineData(false, "https://sqs.eu-west-2.amazonaws.com/123/calculation")]
    public void AddConsumers_WhenLoggingConsumerIsIsolatedOrDisabled_ShouldAcceptConfiguration(
        bool enabled,
        string queueUrl
    )
    {
        var configuration = CreateConfiguration();
        configuration["CalculationAuditEventProcessor:QueueUrl"] =
            "https://sqs.eu-west-2.amazonaws.com/123/calculation";
        configuration["CalculationAuditEventConsumer:QueueUrl"] = queueUrl;
        configuration["CalculationAuditEventConsumer:ProcessingEnabled"] = enabled.ToString();
        var services = new ServiceCollection();
        services.AddConsumers(configuration, false);
        using var provider = services.BuildServiceProvider();

        var options = provider
            .GetRequiredService<IOptionsMonitor<AnalyticsAuditEventConsumerOptions>>()
            .Get(AnalyticsAuditEventConsumerOptions.CalculationSectionName);

        options.QueueUrl.Should().Be(queueUrl);
        options.ProcessingEnabled.Should().Be(enabled);
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
