using System.Net.Http.Headers;
using System.Security.Claims;
using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.AuditEvents.Data;
using Defra.WasteObligations.AuditEvents.Entities;
using Defra.WasteObligations.Testing;
using MongoDB.Driver;
using ServiceCollectionExtensions = Defra.WasteObligations.Api.Data.ServiceCollectionExtensions;

namespace Defra.WasteObligations.Api.IntegrationTests;

[Trait("Category", "IntegrationTests")]
[Collection("Integration Tests")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    public required WireMockContext WireMockContext;

    public required IMongoCollection<ComplianceDeclaration> ComplianceDeclarations { get; set; }
    public required IMongoCollection<AuditEventCounter> AuditEventCounters { get; set; }
    public required IMongoCollection<AuditEvent> AuditEvents { get; set; }
    public required IMongoCollection<AuditEventDispatchLease> AuditEventDispatchLeases { get; set; }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    public async ValueTask InitializeAsync()
    {
        WireMockContext = new WireMockContext();

        await WireMockContext.InitializeAsync();

        ComplianceDeclarations = GetMongoCollection<ComplianceDeclaration>();
        AuditEventCounters = GetMongoCollection<AuditEventCounter>();
        AuditEvents = GetMongoCollection<AuditEvent>();
        AuditEventDispatchLeases = GetMongoCollection<AuditEventDispatchLease>(
            AuditEventDbContext.AuditEventDispatchLeaseCollectionName
        );

        await DeleteMany(ComplianceDeclarations);
        await DeleteMany(AuditEventCounters);
        await DeleteMany(AuditEvents);
        await DeleteMany(AuditEventDispatchLeases);
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
        var settings = MongoClientSettings.FromConnectionString(
            "mongodb://127.0.0.1:27017/?replicaSet=rs0&directConnection=true"
        );
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);
        settings.SocketTimeout = TimeSpan.FromSeconds(5);

        return new MongoClient(settings).GetDatabase("waste-obligations");
    }

    private static IMongoCollection<T> GetMongoCollection<T>() => GetMongoCollection<T>(typeof(T).Name);

    private static IMongoCollection<T> GetMongoCollection<T>(string collectionName) =>
        GetMongoDatabase().GetCollection<T>(collectionName);

    private static async Task DeleteMany<T>(IMongoCollection<T> collection) =>
        await collection.DeleteManyAsync(FilterDefinition<T>.Empty, TestContext.Current.CancellationToken);

    static IntegrationTestBase()
    {
        ServiceCollectionExtensions.RegisterConventions();
    }
}
