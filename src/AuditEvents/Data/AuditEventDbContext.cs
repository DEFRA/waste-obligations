using Defra.WasteObligations.AuditEvents.Entities;
using MongoDB.Driver;

namespace Defra.WasteObligations.AuditEvents.Data;

public class AuditEventDbContext(IMongoDatabase database) : IAuditEventDbContext
{
    public IMongoCollection<AuditEventCounter> AuditEventCounters { get; } =
        database.GetCollection<AuditEventCounter>(nameof(AuditEventCounter));

    public IMongoCollection<AuditEvent> AuditEvents { get; } = database.GetCollection<AuditEvent>(nameof(AuditEvent));

    public IMongoCollection<AuditEventDispatchLease> AuditEventDispatchLeases { get; } =
        database.GetCollection<AuditEventDispatchLease>(nameof(AuditEventDispatchLease));
}
