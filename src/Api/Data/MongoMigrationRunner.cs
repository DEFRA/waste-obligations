using AdaskoTheBeAsT.MongoDbMigrations;
using Defra.WasteObligations.Api.Data.Corrections;
using Defra.WasteObligations.Api.Data.Migrations;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Data;

public class MongoMigrationRunner(
    IMongoDatabase database,
    ILogger<MongoMigrationRunner> logger,
    IServiceScopeFactory serviceScopeFactory
) : IMongoMigrationRunner
{
    public async Task Run(CancellationToken cancellationToken)
    {
        using var engine = new MigrationEngineBuilder().UseDatabase(
            database.Client,
            database.DatabaseNamespace.DatabaseName
        );

        var result = await engine
            .UseAssemblyOfType<ComplianceDeclarationIndexes>()
            .UseSchemeValidation(false)
            .UseAfterMigration(
                (migration, success) =>
                {
                    if (success)
                    {
                        logger.LogInformation(
                            "Mongo migration {MigrationName} version {MigrationVersion} completed.",
                            migration.Name,
                            migration.Version
                        );
                    }
                    else
                    {
                        logger.LogError(
                            "Mongo migration {MigrationName} version {MigrationVersion} failed.",
                            migration.Name,
                            migration.Version
                        );
                    }
                }
            )
            .RunAsync(cancellationToken);

        if (!result.Success)
        {
            throw new InvalidOperationException("Mongo migrations did not complete successfully.");
        }

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDataCorrectionRunner>().Run(cancellationToken);

        logger.LogInformation(
            "Mongo migrations completed. Current version is {CurrentVersion}. Applied {AppliedMigrationCount} migration(s).",
            result.CurrentVersion,
            result.InterimSteps.Count
        );
    }
}
