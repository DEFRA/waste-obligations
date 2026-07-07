using Amazon.SimpleNotificationService;
using Defra.WasteObligations.AuditEvents.Analytics;
using Defra.WasteObligations.AuditEvents.Data;
using Defra.WasteObligations.AuditEvents.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.AuditEvents;

public static class ServiceCollectionExtensions
{
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

        return services;
    }
}
