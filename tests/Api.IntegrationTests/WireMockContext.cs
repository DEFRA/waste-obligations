using RestEase;
using WireMock.Client;

namespace Defra.WasteObligations.Api.IntegrationTests;

public class WireMockContext : IAsyncLifetime
{
    private static string BaseUri => "http://localhost:9090";

    public IWireMockAdminApi WireMockAdminApi { get; } = RestClient.For<IWireMockAdminApi>(BaseUri);

    public async ValueTask InitializeAsync()
    {
        await WireMockAdminApi.ResetMappingsAsync();
        await WireMockAdminApi.ResetRequestsAsync();
        await WireMockAdminApi.ResetScenariosAsync();
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
