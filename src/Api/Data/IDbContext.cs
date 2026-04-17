using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Data;

public interface IDbContext
{
    IMongoCollection<ComplianceDeclaration> ComplianceDeclarations { get; }
}
