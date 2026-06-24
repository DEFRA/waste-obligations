namespace Defra.WasteObligations.AuditEvents;

public class UlidEventIdGenerator : IEventIdGenerator
{
    public string Generate() => Ulid.NewUlid().ToString();
}
