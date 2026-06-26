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
    private const string ServiceUrl = "http://localhost:4566";
    private const string QueueUrl = ServiceUrl + "/000000000000/waste_obligations_analytics_events_queue";

    [Fact]
    public async Task WhenAuditEventCreated_ShouldPublishJsonToSubscribedQueue()
    {
        using var sqsClient = CreateSqsClient();
        await DrainQueue(sqsClient);
        var organisationId = Guid.NewGuid();
        await WireMockContext.WireMockAdminApi.StubWasteOrganisationsOrganisationRequest(
            organisationId,
            BasicAuthCredential.ForClient(ClientIds.WasteOrganisations)
        );
        await WireMockContext.WireMockAdminApi.StubTokenRequest(
            expiryInSeconds: 60,
            clientId: ClientIds.AccountBackend
        );
        var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Create(organisationId),
            CreateComplianceDeclarationRequestFixture.DirectProducer(organisationId).Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var complianceDeclaration = await response.Content.ReadFromJsonAsync<ComplianceDeclaration>(
            TestContext.Current.CancellationToken
        );
        var message = await ReceiveMessage(sqsClient);
        using var deserializedMessage = JsonSerializer.Deserialize<JsonDocument>(message.Body);

        deserializedMessage.Should().NotBeNull();
        complianceDeclaration.Should().NotBeNull();
        var root = deserializedMessage!.RootElement;
        var after = root.GetProperty("after");

        root.GetProperty("eventId").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("entityId").GetString().Should().Be($"compliance_declaration_{complianceDeclaration!.Id}");
        root.GetProperty("operation").GetString().Should().Be("insert");
        root.GetProperty("schemaVersion")
            .GetString()
            .Should()
            .Be($"compliance_declaration.{ComplianceDeclarationEntity.SchemaVersionValue}");
        after.GetProperty("_id").GetString().Should().Be(complianceDeclaration.Id);
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
                        WaitTimeSeconds = 1,
                    },
                    TestContext.Current.CancellationToken
                );

                response.Messages.Should().ContainSingle();
                receivedMessage = response.Messages!.Single();
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
}
