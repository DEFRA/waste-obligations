using System.Diagnostics.CodeAnalysis;
using AdaskoTheBeAsT.MongoDbMigrations;
using Defra.WasteObligations.Api.Data.Migrations;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Data;

[ExcludeFromCodeCoverage(Justification = "See integration tests")]
public class MongoMigrationService(
    IMongoDatabase database,
    TimeProvider timeProvider,
    ILogger<MongoMigrationService> logger
) : IHostedService
{
    private const int DuplicateKeyErrorCode = 11000;
    private const string LeaseCollectionName = "_migrations_lease";
    private const string LeaseId = "mongo-migrations";
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan LeaseRetryDelay = TimeSpan.FromSeconds(5);

    private readonly string _instanceId = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var lease = database.GetCollection<MongoMigrationLease>(LeaseCollectionName);

        logger.LogInformation("Starting Mongo migrations.");

        while (!cancellationToken.IsCancellationRequested)
        {
            var acquired = await TryAcquireLease(lease, cancellationToken);

            if (acquired)
            {
                logger.LogInformation("Mongo migration lease acquired by {InstanceId}.", _instanceId);

                try
                {
                    await RunMigrations(cancellationToken);
                }
                finally
                {
                    await ReleaseLease(lease, cancellationToken);
                    logger.LogInformation("Mongo migration lease released by {InstanceId}.", _instanceId);
                }

                return;
            }

            logger.LogInformation("Mongo migration lease is held by another host. Waiting before retrying.");
            await Task.Delay(LeaseRetryDelay, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<bool> TryAcquireLease(
        IMongoCollection<MongoMigrationLease> lease,
        CancellationToken cancellationToken
    )
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var filter = Builders<MongoMigrationLease>.Filter.And(
            Builders<MongoMigrationLease>.Filter.Eq(x => x.Id, LeaseId),
            Builders<MongoMigrationLease>.Filter.Or(
                Builders<MongoMigrationLease>.Filter.Lte(x => x.ExpiresAt, utcNow),
                Builders<MongoMigrationLease>.Filter.Eq(x => x.Owner, _instanceId)
            )
        );
        var update = Builders<MongoMigrationLease>
            .Update.Set(x => x.Owner, _instanceId)
            .Set(x => x.ExpiresAt, utcNow.Add(LeaseDuration));
        var options = new FindOneAndUpdateOptions<MongoMigrationLease>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After,
        };

        try
        {
            var result = await lease.FindOneAndUpdateAsync(filter, update, options, cancellationToken);

            return result is not null && result.Owner == _instanceId;
        }
        catch (MongoCommandException exception) when (exception.Code == DuplicateKeyErrorCode)
        {
            return false;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Code == DuplicateKeyErrorCode)
        {
            return false;
        }
    }

    private async Task ReleaseLease(IMongoCollection<MongoMigrationLease> lease, CancellationToken cancellationToken)
    {
        var filter = Builders<MongoMigrationLease>.Filter.And(
            Builders<MongoMigrationLease>.Filter.Eq(x => x.Id, LeaseId),
            Builders<MongoMigrationLease>.Filter.Eq(x => x.Owner, _instanceId)
        );

        await lease.DeleteOneAsync(filter, cancellationToken);
    }

    private async Task RunMigrations(CancellationToken cancellationToken)
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

        logger.LogInformation(
            "Mongo migrations completed. Current version is {CurrentVersion}. Applied {AppliedMigrationCount} migration(s).",
            result.CurrentVersion,
            result.InterimSteps.Count
        );
    }
}
