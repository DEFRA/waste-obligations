using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Defra.WasteObligations.AuditEvents.Analytics;
using Defra.WasteObligations.AuditEvents.Data;
using Defra.WasteObligations.AuditEvents.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.AuditEvents;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers transactional capture and two dispatchers: declarations through SNS, calculations directly
    /// to the topic's subscribed queue. The API registers the single shared logging consumer separately.
    /// </summary>
    public static IServiceCollection AddAuditEvents(
        this IServiceCollection services,
        IConfiguration configuration,
        bool addAnalyticsProcessor = true
    )
    {
        services
            .AddOptions<AnalyticsAuditEventProcessorOptions>()
            .Bind(configuration.GetSection(AnalyticsAuditEventProcessorOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                x => !x.ProcessingEnabled || !string.IsNullOrWhiteSpace(x.TopicArn),
                "Topic ARN is required when processing is enabled."
            )
            .Validate(
                x =>
                    !x.ProcessingEnabled
                    || (
                        x.IncludedEntities.Length > 0
                        && x.IncludedEntities.All(AnalyticsEventVocabulary.IsRegisteredEntity)
                    ),
                "Enabled dispatchers require registered included entities."
            )
            .ValidateOnStart();

        services.AddScoped<IAuditEventDbContext, AuditEventDbContext>();
        services.AddTransient<IAuditEventService, AuditEventService>();
        services.AddSingleton<IEventIdGenerator, UlidEventIdGenerator>();
        services.AddScoped<AuditEventLeaseService>();
        services.AddScoped<AuditEventDispatchService>();
        services.AddTransient<IAnalyticsEventSerializer, JsonAnalyticsEventSerializer>();
        services.AddTransient<SnsAnalyticsEventSender>();
        services.AddTransient<IAnalyticsEventSender>(sp => new MetricsAnalyticsEventSender(
            sp.GetRequiredService<SnsAnalyticsEventSender>(),
            sp.GetRequiredService<IAuditEventMetrics>(),
            sp.GetRequiredService<IOptions<AnalyticsAuditEventProcessorOptions>>()
        ));

        services.AddDefaultAWSOptions(configuration.GetAWSOptions());
        services.AddAWSService<IAmazonSimpleNotificationService>();

        if (addAnalyticsProcessor)
        {
            services.AddHostedService<AnalyticsAuditEventProcessor>();
        }

        services.AddAuditEventQueueDispatcher(
            configuration,
            AnalyticsAuditEventProcessorOptions.CalculationSectionName,
            addAnalyticsProcessor
        );

        return services;
    }

    /// <summary>
    /// Registers a dispatcher that sends its included entities directly to an SQS queue. The configuration
    /// section name also names the dispatcher's options.
    /// </summary>
    public static IServiceCollection AddAuditEventQueueDispatcher(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName,
        bool addHostedDispatcher = true
    )
    {
        services
            .AddOptions<AnalyticsAuditEventProcessorOptions>(sectionName)
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .Validate(
                x => !x.ProcessingEnabled || !string.IsNullOrWhiteSpace(x.QueueUrl),
                "Queue URL is required when processing is enabled."
            )
            .Validate(
                x =>
                    !x.ProcessingEnabled
                    || x.ProcessName != configuration[$"{AnalyticsAuditEventProcessorOptions.SectionName}:ProcessName"],
                "Dispatchers must have distinct process names."
            )
            .Validate(
                x =>
                    !x.ProcessingEnabled
                    || (
                        x.IncludedEntities.Length > 0
                        && x.IncludedEntities.All(AnalyticsEventVocabulary.IsRegisteredEntity)
                    ),
                "Enabled dispatchers require registered included entities."
            )
            .ValidateOnStart();
        services.AddAWSService<IAmazonSQS>();
        if (addHostedDispatcher)
        {
            services.AddSingleton<IHostedService>(sp => new AnalyticsAuditEventProcessor(
                sp.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(
                    sp.GetRequiredService<IOptionsMonitor<AnalyticsAuditEventProcessorOptions>>().Get(sectionName)
                ),
                sp.GetRequiredService<IAuditEventMetrics>(),
                sp.GetRequiredService<ILogger<AnalyticsAuditEventProcessor>>(),
                scoped =>
                    ActivatorUtilities.CreateInstance<SqsAnalyticsEventSender>(
                        scoped,
                        Options.Create(
                            sp.GetRequiredService<IOptionsMonitor<AnalyticsAuditEventProcessorOptions>>()
                                .Get(sectionName)
                        )
                    )
            ));
        }

        return services;
    }
}
