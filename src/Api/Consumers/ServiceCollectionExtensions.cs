using Amazon.SQS;

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
}
