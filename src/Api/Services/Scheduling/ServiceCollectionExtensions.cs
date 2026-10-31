using Cronos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services.Scheduling;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddScheduledJobs(
        this IServiceCollection services,
        IConfiguration configuration,
        bool addWorkers = true
    )
    {
        foreach (var job in configuration.GetChildren())
        {
            if (job.GetValue<bool>("Enabled"))
            {
                var jobType = FindType(job.Key, "Job");
                if (!typeof(IScheduledJob).IsAssignableFrom(jobType))
                    throw new InvalidOperationException($"Scheduled job {job.Key} must implement IScheduledJob");
                var cron =
                    job["Cron"] ?? throw new InvalidOperationException($"Cron is required for scheduled job {job.Key}");
                var schedule = CronExpression.Parse(cron);
                services.TryAddSingleton(TimeProvider.System);
                services.AddScoped(jobType);
                if (addWorkers)
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

            if (job.GetValue<bool>("Processing:Enabled"))
                AddProcessor(services, job, addWorkers);
        }

        return services;
    }

    private static void AddProcessor(IServiceCollection services, IConfigurationSection job, bool addWorkers)
    {
        var processorType = FindType(job.Key, "Processor");
        var contracts = processorType
            .GetInterfaces()
            .Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IScheduledJobProcessor<>))
            .ToArray();
        if (contracts.Length != 1)
            throw new InvalidOperationException(
                $"Processor for {job.Key} must implement exactly one IScheduledJobProcessor<TWork>"
            );
        var workType = contracts[0].GetGenericArguments()[0];
        var sourceType = FindType(job.Key, "WorkSource");
        var sourceContract = typeof(IScheduledJobWorkSource<>).MakeGenericType(workType);
        if (!sourceContract.IsAssignableFrom(sourceType))
            throw new InvalidOperationException($"Work source for {job.Key} must match its processor's work type");

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped(contracts[0], processorType);
        services.AddScoped(sourceContract, sourceType);
        services
            .AddOptions<ScheduledJobProcessingOptions>(job.Key)
            .Bind(job.GetSection("Processing"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        if (addWorkers)
            services.AddSingleton<IHostedService>(provider =>
            {
                var options = provider
                    .GetRequiredService<IOptionsMonitor<ScheduledJobProcessingOptions>>()
                    .Get(job.Key);
                Func<string, IScheduledJobLeaseService> leaseFactory = id => new ScheduledJobLeaseService(
                    provider.GetRequiredService<IMongoDatabase>(),
                    provider.GetRequiredService<TimeProvider>(),
                    job.Key,
                    id
                );

                return (IHostedService)
                    ActivatorUtilities.CreateInstance(
                        provider,
                        typeof(ScheduledJobProcessingWorker<>).MakeGenericType(workType),
                        job.Key,
                        options,
                        leaseFactory
                    );
            });
    }

    private static Type FindType(string jobName, string suffix)
    {
        var typeName = $"Defra.WasteObligations.Api.ScheduledJobs.{jobName}{suffix}";
        var type = typeof(IScheduledJob).Assembly.GetType(typeName);
        if (type is null || !type.IsClass || type.IsAbstract || type.ContainsGenericParameters)
            throw new InvalidOperationException($"Scheduled job {jobName} requires a concrete type {typeName}");

        return type;
    }
}
