using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using AwesomeAssertions;
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
    protected const string TraceHeaderName = "x-cdp-request-id";
    protected const string TraceId = "trace-id-1";
    protected const string AnalyticsEventsQueueUrl =
        "http://localhost:4566/000000000000/waste_obligations_analytics_events_queue";

    private const string ContentEncodingHeader = "Content-Encoding";
    private const string ContentTypeHeader = "Content-Type";
    private const string JsonContentType = "application/json";
    private const string ServiceUrl = "http://localhost:4566";

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
        AuditEventCounters = GetMongoCollection<AuditEventCounter>(AuditEventDbContext.AuditEventCounterCollectionName);
        AuditEvents = GetMongoCollection<AuditEvent>();
        AuditEventDispatchLeases = GetMongoCollection<AuditEventDispatchLease>(
            AuditEventDbContext.AuditEventDispatchLeaseCollectionName
        );

        await DeleteMany(ComplianceDeclarations);
        await DeleteMany(AuditEventCounters);
        await DeleteMany(AuditEvents);
        await DeleteMany(AuditEventDispatchLeases);

        using var sqsClient = CreateSqsClient();
        await DrainAnalyticsEventsQueue(sqsClient);
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

    protected static IAmazonSQS CreateSqsClient()
    {
        var config = new AmazonSQSConfig { ServiceURL = ServiceUrl, AuthenticationRegion = "eu-west-2" };
        var credentials = new BasicAWSCredentials("test", "test");

        return new AmazonSQSClient(credentials, config);
    }

    protected static async Task DrainAnalyticsEventsQueue(IAmazonSQS sqsClient)
    {
        while (true)
        {
            var response = await sqsClient.ReceiveMessageAsync(
                new ReceiveMessageRequest
                {
                    QueueUrl = AnalyticsEventsQueueUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 0,
                },
                TestContext.Current.CancellationToken
            );

            if (response.Messages is not { Count: > 0 })
                return;

            foreach (var message in response.Messages)
            {
                await sqsClient.DeleteMessageAsync(
                    AnalyticsEventsQueueUrl,
                    message.ReceiptHandle,
                    TestContext.Current.CancellationToken
                );
            }
        }
    }

    protected static async Task<Message> ReceiveAnalyticsEventsQueueMessage(IAmazonSQS sqsClient)
    {
        Message? receivedMessage = null;
        await AsyncWaiter.WaitForAsync(
            async () =>
            {
                var response = await sqsClient.ReceiveMessageAsync(
                    new ReceiveMessageRequest
                    {
                        QueueUrl = AnalyticsEventsQueueUrl,
                        MaxNumberOfMessages = 1,
                        MessageAttributeNames = ["All"],
                        WaitTimeSeconds = 1,
                    },
                    TestContext.Current.CancellationToken
                );

                response.Messages.Should().ContainSingle();
                if (response.Messages is not { Count: 1 } messages)
                    throw new InvalidOperationException("Expected a single analytics event message.");

                receivedMessage = messages.Single();
                receivedMessage.MessageAttributes.Should().ContainKey(ContentTypeHeader);
                receivedMessage.MessageAttributes[ContentTypeHeader].StringValue.Should().Be(JsonContentType);
                receivedMessage.MessageAttributes.Should().NotContainKey(ContentEncodingHeader);
            },
            timeout: 10,
            delay: TimeSpan.FromMilliseconds(100)
        );

        receivedMessage.Should().NotBeNull();

        await sqsClient.DeleteMessageAsync(
            AnalyticsEventsQueueUrl,
            receivedMessage.ReceiptHandle,
            TestContext.Current.CancellationToken
        );

        return receivedMessage;
    }

    protected static async Task<JsonDocument> ReceiveAnalyticsEventsQueueJsonMessage(IAmazonSQS sqsClient)
    {
        var message = await ReceiveAnalyticsEventsQueueMessage(sqsClient);
        var deserializedMessage = JsonSerializer.Deserialize<JsonDocument>(message.Body);

        deserializedMessage.Should().NotBeNull();

        return deserializedMessage;
    }

    protected static async Task AssertAnalyticsEventQueued(
        IAmazonSQS sqsClient,
        string complianceDeclarationId,
        string operation,
        string eventType
    )
    {
        using var deserializedMessage = await ReceiveAnalyticsEventsQueueJsonMessage(sqsClient);
        var root = deserializedMessage.RootElement;

        root.GetProperty("entityId").GetString().Should().Be($"compliance_declaration_{complianceDeclarationId}");
        root.GetProperty("operation").GetString().Should().Be(operation);
        root.GetProperty("eventType").GetString().Should().Be(eventType);
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
