using WireMock.Server;

namespace Defra.WasteObligations.Testing;

public class WireMockTestBase : IClassFixture<WireMockContext>
{
    protected WireMockServer WireMock { get; }
    protected WireMockContext Context { get; }

    protected WireMockTestBase(WireMockContext context)
    {
        WireMock = context.Server;
        WireMock.Reset();
        Context = context;
    }
}
