using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
    [Fact]
    public async Task WhenAuditEventCreated_ShouldPublishJsonToSubscribedQueue()
    {
        using var sqsClient = CreateSqsClient();
        var client = CreateClient();

        var complianceDeclaration = await CreateComplianceDeclaration(client);
        using var deserializedMessage = await ReceiveAnalyticsEventsQueueJsonMessage(sqsClient);
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
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TraceHeaderName, TraceId);
        var complianceDeclaration = await CreateComplianceDeclaration(client);
        await ReceiveAnalyticsEventsQueueJsonMessage(sqsClient);

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Update(
                complianceDeclaration.Organisation.Id,
                complianceDeclaration.Id
            ),
            UpdateComplianceDeclarationRequestFixture.Accepted().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var deserializedMessage = await ReceiveAnalyticsEventsQueueJsonMessage(sqsClient);
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
        var client = CreateClient();
        var complianceDeclaration = await CreateComplianceDeclaration(client);
        await ReceiveAnalyticsEventsQueueJsonMessage(sqsClient);

        var response = await client.DeleteAsync(
            Testing.Endpoints.ComplianceDeclarations.Delete(complianceDeclaration.Id),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var deserializedMessage = await ReceiveAnalyticsEventsQueueJsonMessage(sqsClient);
        var root = deserializedMessage.RootElement;

        root.GetProperty("entityId").GetString().Should().Be($"compliance_declaration_{complianceDeclaration.Id}");
        root.GetProperty("operation").GetString().Should().Be("delete");
        root.GetProperty("version").GetInt32().Should().Be(2);
        root.TryGetProperty("traceId", out _).Should().BeFalse();
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

        return complianceDeclaration;
    }
}
