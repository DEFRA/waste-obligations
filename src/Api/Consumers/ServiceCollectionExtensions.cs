using Amazon.SQS;
using Defra.WasteObligations.AuditEvents.Analytics;
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
            AnalyticsAuditEventProcessorOptions.CalculationSectionName,
            addHostedConsumers
        );

        return services;
    }

    /// <summary>
    /// Registers a logging consumer for an isolated diagnostic feed, separate from the dispatch destination.
    /// The configuration section name also names the consumer's options.
    /// </summary>
    public static IServiceCollection AddAuditEventQueueConsumer(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName,
        string dispatcherSectionName,
        bool addHostedConsumer = true
    )
    {
        services
            .AddOptions<AnalyticsAuditEventConsumerOptions>(sectionName)
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .Validate(
                x =>
                    !x.ProcessingEnabled
                    || (
                        Uri.TryCreate(
                            configuration[$"{dispatcherSectionName}:QueueUrl"],
                            UriKind.Absolute,
                            out var destination
                        )
                        && (destination.Scheme == Uri.UriSchemeHttp || destination.Scheme == Uri.UriSchemeHttps)
                        && !string.Equals(
                            x.QueueUrl?.TrimEnd('/'),
                            destination.OriginalString.TrimEnd('/'),
                            StringComparison.Ordinal
                        )
                    ),
                "An enabled logging consumer requires an isolated diagnostic queue distinct from the dispatch destination."
            )
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
