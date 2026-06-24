namespace Defra.WasteObligations.Api.Data;

public class UlidEventIdGenerator : IEventIdGenerator
{
    public string Generate() => Ulid.NewUlid().ToString();
}
