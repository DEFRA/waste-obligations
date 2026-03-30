using Defra.WasteObligations.Api.Data;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.IntegrationTests;

public class MongoTestBase : IntegrationTestBase, IAsyncLifetime
{
    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    public ValueTask InitializeAsync()
    {
        return ValueTask.CompletedTask;
    }

    protected static IMongoDatabase GetMongoDatabase()
    {
        var settings = MongoClientSettings.FromConnectionString("mongodb://127.0.0.1:27017");
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);
        settings.SocketTimeout = TimeSpan.FromSeconds(5);

        return new MongoClient(settings).GetDatabase("waste-obligations");
    }

    static MongoTestBase()
    {
        ServiceCollectionExtensions.RegisterConventions();
    }
}
