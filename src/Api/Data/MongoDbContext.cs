using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Data;

public class MongoDbContext(IMongoDatabase database) : IDbContext
{
    public IMongoCollection<ComplianceDeclaration> ComplianceDeclarations { get; } =
        database.GetCollection<ComplianceDeclaration>(nameof(ComplianceDeclaration));
}
