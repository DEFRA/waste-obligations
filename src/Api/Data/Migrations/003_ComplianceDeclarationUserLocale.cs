using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Dtos;
using MongoDB.Bson;
using MongoDB.Driver;
using ComplianceDeclaration = Defra.WasteObligations.Api.Data.Entities.ComplianceDeclaration;
using ComplianceDeclarationStatus = Defra.WasteObligations.Api.Data.Entities.ComplianceDeclarationStatus;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(nameof(ComplianceDeclaration), MigrationDirection.Both)]
public class ComplianceDeclarationUserLocale : MongoMigration
{
    private const string SchemaVersionField = "schemaVersion";
    private const string SchemaVersionV1 = "v1.0";
    private const string SchemaVersionV1_1 = ComplianceDeclaration.SchemaVersionValue;
    private const string SubmittedAction = nameof(ComplianceDeclarationStatus.Submitted);

    public override MigrationVersion Version => new(1, 0, 2);

    public override string Name => "003 - ComplianceDeclaration user locale";

    public override async Task UpAsync(MigrationContext context)
    {
        var collection = context.Database.GetCollection<BsonDocument>(nameof(ComplianceDeclaration));
        var filter = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Eq(SchemaVersionField, SchemaVersionV1),
            Builders<BsonDocument>.Filter.Exists(SchemaVersionField, false)
        );
        var update = Builders<BsonDocument>
            .Update.Set(SchemaVersionField, SchemaVersionV1_1)
            .Set("audit.$[submittedWithoutLocale].user.locale", UserLocale.En);

        var options = new UpdateOptions
        {
            ArrayFilters =
            [
                new BsonDocumentArrayFilterDefinition<BsonDocument>(
                    new BsonDocument
                    {
                        ["submittedWithoutLocale.action"] = SubmittedAction,
                        ["$or"] = new BsonArray
                        {
                            new BsonDocument("submittedWithoutLocale.user.locale", new BsonDocument("$exists", false)),
                            new BsonDocument("submittedWithoutLocale.user.locale", BsonNull.Value),
                        },
                    }
                ),
            ],
        };

        await collection.UpdateManyAsync(filter, update, options, context.CancellationToken);
    }

    public override async Task DownAsync(MigrationContext context)
    {
        var collection = context.Database.GetCollection<BsonDocument>(nameof(ComplianceDeclaration));
        var filter = Builders<BsonDocument>.Filter.Eq(SchemaVersionField, SchemaVersionV1_1);
        var update = Builders<BsonDocument>
            .Update.Set(SchemaVersionField, SchemaVersionV1)
            .Unset("audit.$[submitted].user.locale");

        var options = new UpdateOptions
        {
            ArrayFilters =
            [
                new BsonDocumentArrayFilterDefinition<BsonDocument>(
                    new BsonDocument("submitted.action", SubmittedAction)
                ),
            ],
        };

        await collection.UpdateManyAsync(filter, update, options, context.CancellationToken);
    }
}
