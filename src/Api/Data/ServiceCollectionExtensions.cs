using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using MongoDB.Driver.Authentication.AWS;

namespace Defra.WasteObligations.Api.Data;

[ExcludeFromCodeCoverage(Justification = "See integration tests")]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDbContext(
        this IServiceCollection services,
        IConfiguration configuration,
        bool validateConfigOnly
    )
    {
        services
            .AddOptions<MongoDbOptions>()
            .Bind(configuration.GetSection(MongoDbOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services
            .AddOptions<MongoMigrationOptions>()
            .BindConfiguration(MongoMigrationOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(
                options => options.LeaseRenewalIntervalSeconds * 2 <= options.LeaseDurationSeconds,
                "Mongo migration lease renewal interval must be no more than half the lease duration"
            )
            .Validate(
                x => x.LeaseRenewalIntervalSeconds + x.LeaseOperationTimeoutSeconds < x.LeaseDurationSeconds,
                "Mongo migration lease operation timeout must leave time for renewal before lease expiry"
            )
            .ValidateOnStart();

        services
            .AddOptions<ApplicationDataCorrectionOptions>()
            .BindConfiguration(ApplicationDataCorrectionOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(
                x => x.LeaseRenewalIntervalSeconds * 2 <= x.LeaseDurationSeconds,
                "Application data correction lease renewal interval must be no more than half its duration"
            )
            .Validate(
                x => x.LeaseRenewalIntervalSeconds + x.LeaseOperationTimeoutSeconds < x.LeaseDurationSeconds,
                "Application data correction lease operation timeout must leave time for renewal before lease expiry"
            )
            .ValidateOnStart();

        if (validateConfigOnly)
            return services;

        services.AddSingleton<IMongoMigrationLeaseService, MongoMigrationLeaseService>();
        services.AddSingleton<IMongoMigrationRunner, MongoMigrationRunner>();
        services.AddSingleton<MongoMigrationCompletion>();
        services.AddHostedService<MongoMigrationService>();
        services.AddSingleton<IApplicationDataCorrectionLeaseService, ApplicationDataCorrectionLeaseService>();
        services.AddScoped<IApplicationDataCorrectionRunner, ApplicationDataCorrectionRunner>();
        services.AddHostedService<ApplicationDataCorrectionWorker>();
        services.AddScoped<IDbContext, MongoDbContext>();
        services.AddSingleton(sp =>
        {
            MongoClientSettings.Extensions.AddAWSAuthentication();

            var options = sp.GetRequiredService<IOptions<MongoDbOptions>>();
            var settings = MongoClientSettings.FromConnectionString(options.Value.DatabaseUri);
            settings.ApplicationName = "waste-obligations-api";
            var client = new MongoClient(settings);

            RegisterConventions();

            return client.GetDatabase(options.Value.DatabaseName);
        });

        return services;
    }

    public static void RegisterConventions()
    {
        var conventionPack = new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new EnumRepresentationConvention(BsonType.String),
        };

        ConventionRegistry.Register(nameof(conventionPack), conventionPack, _ => true);
    }
}
