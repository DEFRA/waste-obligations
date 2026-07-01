using Defra.WasteObligations.AuditEvents.Entities;
using MongoDB.Driver;

namespace Defra.WasteObligations.AuditEvents.Data;

public class AuditEventDbContext(IMongoDatabase database) : IAuditEventDbContext
{
    public const string AuditEventCounterCollectionName = "_audit_event_counter";
    public const string AuditEventDispatchLeaseCollectionName = "_audit_event_dispatch_lease";

    public IMongoCollection<AuditEventCounter> AuditEventCounters { get; } =
        database.GetCollection<AuditEventCounter>(AuditEventCounterCollectionName);

    public IMongoCollection<AuditEvent> AuditEvents { get; } = database.GetCollection<AuditEvent>(nameof(AuditEvent));

    public IMongoCollection<AuditEventDispatchLease> AuditEventDispatchLeases { get; } =
        database.GetCollection<AuditEventDispatchLease>(AuditEventDispatchLeaseCollectionName);
}
