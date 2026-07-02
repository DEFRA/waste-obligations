using MongoDB.Driver;

namespace Defra.WasteObligations.AuditEvents;

public interface IAuditEventService
{
    Task RecordEvent(IClientSessionHandle session, AuditEventRequest auditEvent, CancellationToken cancellationToken);
}
