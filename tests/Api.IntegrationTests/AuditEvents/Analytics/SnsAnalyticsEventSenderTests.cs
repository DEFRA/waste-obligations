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
        using var deserializedMessage = await ReceiveAnalyticsEventsQueueJsonMessage(
            sqsClient,
            MatchAnalyticsEvent(complianceDeclaration.Id, "create", "submission.created")
        );
        var root = deserializedMessage.RootElement;

        root.GetProperty("eventId").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("entityId").GetString().Should().Be($"compliance_declaration_{complianceDeclaration.Id}");
        root.GetProperty("operation").GetString().Should().Be("create");
        root.GetProperty("eventType").GetString().Should().Be("submission.created");
        root.GetProperty("actor").GetString().Should().Be("user:e72be574-8b5b-4836-af47-dd7e0c0d1d87");
        root.GetProperty("deletedReason").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("piiKeyRef").ValueKind.Should().Be(JsonValueKind.Null);
        root.TryGetProperty("correlationId", out _).Should().BeFalse();
        root.GetProperty("schemaVersion")
            .GetString()
            .Should()
            .Be($"compliance_declaration_{ComplianceDeclarationEntity.SchemaVersionValue}");
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
        await ReceiveAnalyticsEventsQueueJsonMessage(
            sqsClient,
            MatchAnalyticsEvent(complianceDeclaration.Id, "create", "submission.created")
        );

        var response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Update(
                complianceDeclaration.Organisation.Id,
                complianceDeclaration.Id
            ),
            UpdateComplianceDeclarationRequestFixture.Accepted().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var deserializedMessage = await ReceiveAnalyticsEventsQueueJsonMessage(
            sqsClient,
            MatchAnalyticsEvent(complianceDeclaration.Id, "update", "submission.amended")
        );
        var root = deserializedMessage.RootElement;

        root.GetProperty("entityId").GetString().Should().Be($"compliance_declaration_{complianceDeclaration.Id}");
        root.GetProperty("operation").GetString().Should().Be("update");
        root.GetProperty("eventType").GetString().Should().Be("submission.amended");
        root.GetProperty("actor").GetString().Should().Be("user:7e91f2ac-5b44-4c8d-ae73-1d9f62b8e0f4");
        root.GetProperty("deletedReason").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("piiKeyRef").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("correlationId").GetString().Should().Be(TraceId);
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
        await ReceiveAnalyticsEventsQueueJsonMessage(
            sqsClient,
            MatchAnalyticsEvent(complianceDeclaration.Id, "create", "submission.created")
        );

        var response = await client.DeleteAsync(
            Testing.Endpoints.ComplianceDeclarations.Delete(complianceDeclaration.Id),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var deserializedMessage = await ReceiveAnalyticsEventsQueueJsonMessage(
            sqsClient,
            MatchAnalyticsEvent(complianceDeclaration.Id, "delete", "submission.removed")
        );
        var root = deserializedMessage.RootElement;

        root.GetProperty("entityId").GetString().Should().Be($"compliance_declaration_{complianceDeclaration.Id}");
        root.GetProperty("operation").GetString().Should().Be("delete");
        root.GetProperty("eventType").GetString().Should().Be("submission.removed");
        root.GetProperty("actor").GetString().Should().Be("service:waste-obligations");
        root.GetProperty("deletedReason").GetString().Should().Be("elevated_system_allowed_removal");
        root.GetProperty("piiKeyRef").ValueKind.Should().Be(JsonValueKind.Null);
        root.TryGetProperty("correlationId", out _).Should().BeFalse();
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
