using Defra.WasteObligations.AuditEvents.Entities;
using MongoDB.Driver;

namespace Defra.WasteObligations.AuditEvents;

public static class AuditEventIndexes
{
    private const string AnalyticsProcessName = "analytics";

    public static IEnumerable<AuditEventIndex> All()
    {
        yield return new AuditEventIndex("Sequence", Builders<AuditEvent>.IndexKeys.Ascending(x => x.Sequence), true);

        yield return new AuditEventIndex(
            "Entity_EntityId_Version",
            Builders<AuditEvent>.IndexKeys.Ascending(x => x.Entity).Ascending(x => x.EntityId).Ascending(x => x.Version)
        );

        yield return new AuditEventIndex(
            $"Dispatch_{AnalyticsProcessName}",
            Builders<AuditEvent>
                .IndexKeys.Ascending(AuditEventDispatchFieldNames.DispatchPath(AnalyticsProcessName))
                .Ascending(x => x.Sequence)
        );
    }
}
