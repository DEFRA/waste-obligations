using Defra.WasteObligations.AuditEvents.Entities;
using MongoDB.Driver;

namespace Defra.WasteObligations.AuditEvents;

public record AuditEventIndex(string Name, IndexKeysDefinition<AuditEvent> Keys, bool Unique = false);
