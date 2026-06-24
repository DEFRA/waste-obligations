using Defra.WasteObligations.AuditEvents.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Defra.WasteObligations.AuditEvents;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuditEvents(this IServiceCollection services)
    {
        services.AddScoped<IAuditEventDbContext, AuditEventDbContext>();
        services.AddTransient<IAuditEventService, AuditEventService>();
        services.AddSingleton<IEventIdGenerator, UlidEventIdGenerator>();

        return services;
    }
}
