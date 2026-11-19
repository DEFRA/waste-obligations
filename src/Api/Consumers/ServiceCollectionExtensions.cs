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

        return services;
    }

    public static IServiceCollection AddAuditEventQueueConsumer(
        this IServiceCollection services,
        IConfiguration configuration,
        string name,
        string sectionName,
        bool addHostedConsumer = true
    )
    {
        services
            .AddOptions<AnalyticsAuditEventConsumerOptions>(name)
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAWSService<IAmazonSQS>();
        if (addHostedConsumer)
        {
            services.AddSingleton<IHostedService>(sp => new AnalyticsAuditEventConsumer(
                sp.GetRequiredService<IAmazonSQS>(),
                Options.Create(sp.GetRequiredService<IOptionsMonitor<AnalyticsAuditEventConsumerOptions>>().Get(name)),
                sp.GetRequiredService<ILogger<AnalyticsAuditEventConsumer>>()
            ));
        }

        return services;
    }
}
