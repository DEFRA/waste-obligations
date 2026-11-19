using Amazon.SQS;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Consumers;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConsumers(
        this IServiceCollection services,
        IConfiguration configuration,
        bool addHostedConsumers = true
    )
    {
        services
            .AddOptions<AnalyticsAuditEventConsumerOptions>()
            .Bind(configuration.GetSection(AnalyticsAuditEventConsumerOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAWSService<IAmazonSQS>();

        if (addHostedConsumers)
        {
            services.AddHostedService<AnalyticsAuditEventConsumer>();
        }

        services.AddAuditEventQueueConsumer(
            configuration,
            AnalyticsAuditEventConsumerOptions.CalculationSectionName,
            addHostedConsumers
        );

        return services;
    }

    /// <summary>
    /// Registers a logging consumer for a directly dispatched queue. The configuration section name also
    /// names the consumer's options.
    /// </summary>
    public static IServiceCollection AddAuditEventQueueConsumer(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName,
        bool addHostedConsumer = true
    )
    {
        services
            .AddOptions<AnalyticsAuditEventConsumerOptions>(sectionName)
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAWSService<IAmazonSQS>();
        if (addHostedConsumer)
        {
            services.AddSingleton<IHostedService>(sp => new AnalyticsAuditEventConsumer(
                sp.GetRequiredService<IAmazonSQS>(),
                Options.Create(
                    sp.GetRequiredService<IOptionsMonitor<AnalyticsAuditEventConsumerOptions>>().Get(sectionName)
                ),
                sp.GetRequiredService<ILogger<AnalyticsAuditEventConsumer>>()
            ));
        }

        return services;
    }
}
