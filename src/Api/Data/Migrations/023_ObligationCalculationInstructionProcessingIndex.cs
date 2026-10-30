using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(ObligationCalculationInstruction.CollectionName, MigrationDirection.Both)]
public class ObligationCalculationInstructionProcessingIndex : MongoMigration
{
    private const string IndexName = "Status_CreatedAt_Id";

    public override MigrationVersion Version => new(1, 0, 22);

    public override string Name => "023 - Obligation calculation instruction processing index";

    public override Task UpAsync(MigrationContext context) =>
        CreateIndex(
            context,
            ObligationCalculationInstruction.CollectionName,
            IndexName,
            Builders<ObligationCalculationInstruction>
                .IndexKeys.Ascending(x => x.Status)
                .Ascending(x => x.CreatedAt)
                .Ascending(x => x.Id)
        );

    public override Task DownAsync(MigrationContext context) =>
        DropIndex<ObligationCalculationInstruction>(
            context,
            ObligationCalculationInstruction.CollectionName,
            IndexName
        );
}
