using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Data;

public interface IDbContext
{
    IMongoCollection<ComplianceDeclaration> ComplianceDeclarations { get; }
    IMongoCollection<AuditEventCounter> AuditEventCounters { get; }
    IMongoCollection<AuditEvent> AuditEvents { get; }

    Task<IClientSessionHandle> StartSession(CancellationToken cancellationToken);
}
