using Cronos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services.Scheduling;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddScheduledJobs(this IServiceCollection services, IConfiguration configuration)
    {
        foreach (var job in configuration.GetChildren().Where(x => x.GetValue<bool>("Enabled")))
        {
            var typeName = $"Defra.WasteObligations.Api.ScheduledJobs.{job.Key}Job";
            var jobType = typeof(IScheduledJob).Assembly.GetType(typeName);
            if (
                jobType is null
                || !jobType.IsClass
                || jobType.IsAbstract
                || jobType.ContainsGenericParameters
                || !typeof(IScheduledJob).IsAssignableFrom(jobType)
            )
                throw new InvalidOperationException(
                    $"Scheduled job {job.Key} requires a concrete IScheduledJob type {typeName}"
                );

            var cron =
                job["Cron"] ?? throw new InvalidOperationException($"Cron is required for scheduled job {job.Key}");
            var schedule = CronExpression.Parse(cron);
            services.TryAddSingleton(TimeProvider.System);
            services.AddScoped(jobType);
            services.AddSingleton<IHostedService>(provider => new ScheduledJobWorker(
                job.Key,
                schedule,
                jobType,
                provider.GetRequiredService<IServiceScopeFactory>(),
                provider.GetRequiredService<IMongoDatabase>(),
                new ScheduledJobLeaseService(
                    provider.GetRequiredService<IMongoDatabase>(),
                    provider.GetRequiredService<TimeProvider>(),
                    job.Key
                ),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<ILogger<ScheduledJobWorker>>()
            ));
        }

        return services;
    }
}
