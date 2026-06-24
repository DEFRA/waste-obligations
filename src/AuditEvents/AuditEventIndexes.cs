using Defra.WasteObligations.AuditEvents.Entities;
using MongoDB.Driver;

namespace Defra.WasteObligations.AuditEvents;

public static class AuditEventIndexes
{
    public static IEnumerable<AuditEventIndex> All()
    {
        yield return new AuditEventIndex("Sequence", Builders<AuditEvent>.IndexKeys.Ascending(x => x.Sequence), true);

        yield return new AuditEventIndex(
            "Entity_EntityId_Version",
            Builders<AuditEvent>.IndexKeys.Ascending(x => x.Entity).Ascending(x => x.EntityId).Ascending(x => x.Version)
        );
    }
}
