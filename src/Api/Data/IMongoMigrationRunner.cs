namespace Defra.WasteObligations.Api.Data;

public interface IMongoMigrationRunner
{
    Task Run(CancellationToken cancellationToken);
}
