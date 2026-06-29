namespace Defra.WasteObligations.AuditEvents.Analytics;

public interface IEntityJsonSchemaProvider
{
    JsonSchemaDocument Get(string entity, string schemaVersion);
}
