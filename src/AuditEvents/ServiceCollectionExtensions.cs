using Defra.WasteObligations.AuditEvents.Analytics;
using Defra.WasteObligations.AuditEvents.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddTransient<IAnalyticsEventSender, LoggingAnalyticsEventSender>();

        if (addAnalyticsProcessor)
        {
            services.AddHostedService<AnalyticsAuditEventProcessor>();
        }

        return services;
    }
}
