using Defra.WasteObligations.AuditEvents;

namespace Defra.WasteObligations.Testing.Fakes;

public class FakeEventIdGenerator : IEventIdGenerator
{
    private long _sequence;

    public string Generate()
    {
        _sequence++;

        return $"01HXYZ{_sequence:00000000000000000000}";
    }
}
