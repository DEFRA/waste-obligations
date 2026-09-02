using AdaskoTheBeAsT.MongoDbMigrations.Abstractions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;
using ComplianceDeclaration = Defra.WasteObligations.Api.Data.Entities.ComplianceDeclaration;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Data.Migrations;

[MigrationCollection(nameof(ComplianceDeclaration), MigrationDirection.Both)]
public class ComplianceDeclarationBusinessCountry : MongoMigration
{
    private const string BusinessCountryField = "organisation.businessCountry";
    private const string SchemaVersionField = "schemaVersion";
    private const string SchemaVersionV1_2 = "v1.2";
    private const string SchemaVersionV1_3 = "v1.3";

    public override MigrationVersion Version => new(1, 0, 7);

    public override string Name => "008 - ComplianceDeclaration business country";

    public override async Task UpAsync(MigrationContext context)
    {
        var collection = context.Database.GetCollection<BsonDocument>(nameof(ComplianceDeclaration));
        var filter = Builders<BsonDocument>.Filter.Eq(SchemaVersionField, SchemaVersionV1_2);
        var update = Builders<BsonDocument>.Update.Set(SchemaVersionField, SchemaVersionV1_3);

        await collection.UpdateManyAsync(filter, update, cancellationToken: context.CancellationToken);
    }

    public override async Task DownAsync(MigrationContext context)
    {
        var collection = context.Database.GetCollection<BsonDocument>(nameof(ComplianceDeclaration));
        var filter = Builders<BsonDocument>.Filter.Eq(SchemaVersionField, SchemaVersionV1_3);
        var update = Builders<BsonDocument>
            .Update.Set(SchemaVersionField, SchemaVersionV1_2)
            .Unset(BusinessCountryField);

        await collection.UpdateManyAsync(filter, update, cancellationToken: context.CancellationToken);
    }
}
