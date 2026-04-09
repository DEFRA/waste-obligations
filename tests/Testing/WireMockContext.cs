using System.Diagnostics.CodeAnalysis;
using WireMock.Server;

namespace Defra.WasteObligations.Testing;

[SuppressMessage("Major Code Smell", "S3881:\"IDisposable\" should be implemented correctly")]
public class WireMockContext : IDisposable
{
    public WireMockServer Server { get; }
    public string BaseAddress { get; }
    public HttpClient HttpClient { get; }

    public WireMockContext()
    {
        Server = WireMockServer.Start();
        BaseAddress = Server.Urls[0];
        HttpClient = new HttpClient { BaseAddress = new Uri(BaseAddress) };
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Server.Stop();
        Server.Dispose();
        HttpClient.Dispose();
    }
}
