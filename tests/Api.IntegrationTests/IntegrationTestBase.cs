using System.Net.Http.Headers;
using System.Security.Claims;
using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Testing;
using MongoDB.Driver;
using ServiceCollectionExtensions = Defra.WasteObligations.Api.Data.ServiceCollectionExtensions;

namespace Defra.WasteObligations.Api.IntegrationTests;

[Trait("Category", "IntegrationTests")]
[Collection("Integration Tests")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    public required WireMockContext WireMockContext;

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    public async ValueTask InitializeAsync()
    {
        WireMockContext = new WireMockContext();

        await WireMockContext.InitializeAsync();
    }

    protected static HttpClient CreateClient()
    {
        var client = new HttpClient { BaseAddress = new Uri("http://localhost:8080") };

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtAuthenticationHandler.SchemeName,
            // See compose.yml for configuration of IntegrationTest client
            GenerateJwt("IntegrationTest")
        );

        return client;
    }

    private static string GenerateJwt(string clientId)
    {
        var claims = new[] { new Claim(Claims.ClientId, clientId) };

        return Jwt.GenerateJwt(claims);
    }

    protected static IMongoDatabase GetMongoDatabase()
    {
        var settings = MongoClientSettings.FromConnectionString("mongodb://127.0.0.1:27017");
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);
        settings.SocketTimeout = TimeSpan.FromSeconds(5);

        return new MongoClient(settings).GetDatabase("waste-obligations");
    }

    static IntegrationTestBase()
    {
        ServiceCollectionExtensions.RegisterConventions();
    }
}
