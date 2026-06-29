using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Testing.Authentication;
using Defra.WasteObligations.Testing.Extensions.WireMock;
using Defra.WasteObligations.Testing.Fixtures.Dtos;
using ComplianceDeclaration = Defra.WasteObligations.Api.Dtos.ComplianceDeclaration;
using ComplianceDeclarationEntity = Defra.WasteObligations.Api.Data.Entities.ComplianceDeclaration;

namespace Defra.WasteObligations.Api.IntegrationTests.AuditEvents.Analytics;

public class SnsAnalyticsEventSenderTests : IntegrationTestBase
{
    private const string ContentEncodingHeader = "Content-Encoding";
    private const string ContentTypeHeader = "Content-Type";
    private const string ServiceUrl = "http://localhost:4566";
    private const string QueueUrl = ServiceUrl + "/000000000000/waste_obligations_analytics_events_queue";

    [Fact]
    public async Task WhenAuditEventCreated_ShouldPublishJsonToSubscribedQueue()
    {
        using var sqsClient = CreateSqsClient();
        await DrainQueue(sqsClient);
        var client = CreateClient();

        var complianceDeclaration = await CreateComplianceDeclaration(client);
        using var deserializedMessage = await ReceiveJsonMessage(sqsClient);
        var root = deserializedMessage.RootElement;

        root.GetProperty("eventId").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("entityId").GetString().Should().Be($"compliance_declaration_{complianceDeclaration.Id}");
        root.GetProperty("operation").GetString().Should().Be("insert");
        root.GetProperty("schemaVersion")
            .GetString()
            .Should()
            .Be($"compliance_declaration.{ComplianceDeclarationEntity.SchemaVersionValue}");
        root.GetProperty("before").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("after").GetProperty("id").GetString().Should().Be(complianceDeclaration.Id);
    }

    [Fact]
    public async Task WhenAuditEventUpdated_ShouldPublishJsonToSubscribedQueue()
    {
        using var sqsClient = CreateSqsClient();
        await DrainQueue(sqsClient);
        var client = CreateClient();
        var complianceDeclaration = await CreateComplianceDeclaration(client);
        await ReceiveJsonMessage(sqsClient);

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Update(
                complianceDeclaration.Organisation.Id,
                complianceDeclaration.Id
            ),
            UpdateComplianceDeclarationRequestFixture.Accepted().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var deserializedMessage = await ReceiveJsonMessage(sqsClient);
        var root = deserializedMessage.RootElement;

        root.GetProperty("entityId").GetString().Should().Be($"compliance_declaration_{complianceDeclaration.Id}");
        root.GetProperty("operation").GetString().Should().Be("update");
        root.GetProperty("version").GetInt32().Should().Be(2);
        root.GetProperty("before").GetProperty("status").GetString().Should().Be("Submitted");
        root.GetProperty("after").GetProperty("status").GetString().Should().Be("Accepted");
    }

    [Fact]
    public async Task WhenAuditEventDeleted_ShouldPublishJsonToSubscribedQueue()
    {
        using var sqsClient = CreateSqsClient();
        await DrainQueue(sqsClient);
        var client = CreateClient();
        var complianceDeclaration = await CreateComplianceDeclaration(client);
        await ReceiveJsonMessage(sqsClient);

        var response = await client.DeleteAsync(
            Testing.Endpoints.ComplianceDeclarations.Delete(complianceDeclaration.Id),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var deserializedMessage = await ReceiveJsonMessage(sqsClient);
        var root = deserializedMessage.RootElement;

        root.GetProperty("entityId").GetString().Should().Be($"compliance_declaration_{complianceDeclaration.Id}");
        root.GetProperty("operation").GetString().Should().Be("delete");
        root.GetProperty("version").GetInt32().Should().Be(2);
        root.GetProperty("before").GetProperty("id").GetString().Should().Be(complianceDeclaration.Id);
        root.GetProperty("after").ValueKind.Should().Be(JsonValueKind.Null);
    }

    private async Task<ComplianceDeclaration> CreateComplianceDeclaration(HttpClient client)
    {
        var organisationId = Guid.NewGuid();
        await WireMockContext.WireMockAdminApi.StubWasteOrganisationsOrganisationRequest(
            organisationId,
            BasicAuthCredential.ForClient(ClientIds.WasteOrganisations)
        );
        await WireMockContext.WireMockAdminApi.StubTokenRequest(
            expiryInSeconds: 60,
            clientId: ClientIds.AccountBackend
        );

        var response = await client.PostAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Create(organisationId),
            CreateComplianceDeclarationRequestFixture.DirectProducer(organisationId).Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var complianceDeclaration = await response.Content.ReadFromJsonAsync<ComplianceDeclaration>(
            TestContext.Current.CancellationToken
        );

        complianceDeclaration.Should().NotBeNull();

        return complianceDeclaration!;
    }

    private static IAmazonSQS CreateSqsClient()
    {
        var config = new AmazonSQSConfig { ServiceURL = ServiceUrl, AuthenticationRegion = "eu-west-2" };
        var credentials = new BasicAWSCredentials("test", "test");

        return new AmazonSQSClient(credentials, config);
    }

    private static async Task DrainQueue(IAmazonSQS sqsClient)
    {
        while (true)
        {
            var response = await sqsClient.ReceiveMessageAsync(
                new ReceiveMessageRequest
                {
                    QueueUrl = QueueUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 1,
                },
                TestContext.Current.CancellationToken
            );

            if (response.Messages is not { Count: > 0 })
                return;

            foreach (var message in response.Messages)
            {
                await sqsClient.DeleteMessageAsync(
                    QueueUrl,
                    message.ReceiptHandle,
                    TestContext.Current.CancellationToken
                );
            }
        }
    }

    private static async Task<Message> ReceiveMessage(IAmazonSQS sqsClient)
    {
        Message? receivedMessage = null;
        await AsyncWaiter.WaitForAsync(
            async () =>
            {
                var response = await sqsClient.ReceiveMessageAsync(
                    new ReceiveMessageRequest
                    {
                        QueueUrl = QueueUrl,
                        MaxNumberOfMessages = 1,
                        MessageAttributeNames = ["All"],
                        WaitTimeSeconds = 1,
                    },
                    TestContext.Current.CancellationToken
                );

                response.Messages.Should().ContainSingle();
                receivedMessage = response.Messages!.Single();
                receivedMessage.MessageAttributes.Should().ContainKey(ContentTypeHeader);
                receivedMessage.MessageAttributes[ContentTypeHeader].StringValue.Should().Be("application/json");
                receivedMessage.MessageAttributes.Should().NotContainKey(ContentEncodingHeader);
            },
            timeout: 10,
            delay: TimeSpan.FromMilliseconds(100)
        );

        await sqsClient.DeleteMessageAsync(
            QueueUrl,
            receivedMessage!.ReceiptHandle,
            TestContext.Current.CancellationToken
        );

        return receivedMessage;
    }

    private static async Task<JsonDocument> ReceiveJsonMessage(IAmazonSQS sqsClient)
    {
        var message = await ReceiveMessage(sqsClient);
        var deserializedMessage = JsonSerializer.Deserialize<JsonDocument>(message.Body);

        deserializedMessage.Should().NotBeNull();

        return deserializedMessage!;
    }
}
