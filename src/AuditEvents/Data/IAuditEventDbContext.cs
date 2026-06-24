using Defra.WasteObligations.AuditEvents.Entities;
using MongoDB.Driver;

namespace Defra.WasteObligations.AuditEvents.Data;

public interface IAuditEventDbContext
{
    IMongoCollection<AuditEventCounter> AuditEventCounters { get; }
    IMongoCollection<AuditEvent> AuditEvents { get; }
}
