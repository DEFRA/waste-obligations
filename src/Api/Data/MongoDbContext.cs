using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Data;

public class MongoDbContext(IMongoDatabase database) : IDbContext
{
    public IMongoCollection<ComplianceDeclaration> ComplianceDeclarations { get; } =
        database.GetCollection<ComplianceDeclaration>(nameof(ComplianceDeclaration));

    public IMongoCollection<AuditEventCounter> AuditEventCounters { get; } =
        database.GetCollection<AuditEventCounter>(nameof(AuditEventCounter));

    public IMongoCollection<AuditEvent> AuditEvents { get; } = database.GetCollection<AuditEvent>(nameof(AuditEvent));

    public async Task<IClientSessionHandle> StartSession(CancellationToken cancellationToken) =>
        await database.Client.StartSessionAsync(cancellationToken: cancellationToken);
}
