using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using Defra.WasteObligations.AuditEvents.Logging;
using Defra.WasteObligations.Testing.Authentication;
using Defra.WasteObligations.Testing.Extensions.WireMock;
using Defra.WasteObligations.Testing.Fixtures.Dtos;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
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
        root.GetProperty("entityId").GetString().Should().Be($"cdec_{complianceDeclaration.Id}");
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

        root.GetProperty("entityId").GetString().Should().Be($"cdec_{complianceDeclaration.Id}");
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

        root.GetProperty("entityId").GetString().Should().Be($"cdec_{complianceDeclaration.Id}");
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

    [Fact]
    public async Task WhenObligationCalculationCreatedAndUpdated_ShouldPublishCalculationEventsToSubscribedQueue()
    {
        using var sqsClient = CreateSqsClient();
        var service = new ObligationCalculationService(
            new MongoDbContext(
                GetMongoApplicationDatabase(),
                Options.Create(new MongoDbOptions()),
                NullLogger<MongoDbContext>.Instance
            ),
            CreateAuditEventService()
        );
        var input = ObligationCalculationFixture.Default().With(x => x.Id, ObjectId.Empty).Create();
        var created = await SaveWithCorrelation(service, input);
        var entityId = $"oblcalc_{created.Id}";

        using (
            var createMessage = await ReceiveAnalyticsEventsQueueJsonMessage(
                sqsClient,
                MatchEntityEvent(entityId, "create", "obligation_calculation.created")
            )
        )
        {
            var root = createMessage.RootElement;
            AssertCalculationEnvelope(root, entityId, 1);
            root.GetProperty("occurredAt").GetString().Should().Be("2026-01-02T03:04:05.000Z");
            root.GetProperty("before").ValueKind.Should().Be(JsonValueKind.Null);
            var after = root.GetProperty("after");
            after.GetProperty("id").GetString().Should().Be(created.Id.ToString());
            after.GetProperty("version").GetInt32().Should().Be(1);
            after
                .GetProperty("submitterOrganisationId")
                .GetString()
                .Should()
                .Be(created.SubmitterOrganisationId.ToString());
            after
                .GetProperty("producers")[0]
                .GetProperty("materials")
                .EnumerateArray()
                .Select(x => x.GetProperty("materialCode").GetString())
                .Should()
                .Equal("PL", "GL", "GR");
        }

        var firstMaterial = created.Producers[0].Materials[0];
        var updated = await SaveWithCorrelation(
            service,
            created with
            {
                CalculatedOn = created.CalculatedOn.AddMinutes(1),
                Producers =
                [
                    created.Producers[0] with
                    {
                        Materials =
                        [
                            firstMaterial with
                            {
                                InputKilograms = firstMaterial.InputKilograms + 100,
                            },
                            .. created.Producers[0].Materials.Skip(1),
                        ],
                    },
                    .. created.Producers.Skip(1),
                ],
            }
        );

        using var updateMessage = await ReceiveAnalyticsEventsQueueJsonMessage(
            sqsClient,
            MatchEntityEvent(entityId, "update", "obligation_calculation.updated")
        );
        var updateRoot = updateMessage.RootElement;
        AssertCalculationEnvelope(updateRoot, entityId, 2);
        updated.Id.Should().Be(created.Id);
        var before = updateRoot.GetProperty("before");
        var updatedAfter = updateRoot.GetProperty("after");
        before.GetProperty("version").GetInt32().Should().Be(1);
        updatedAfter.GetProperty("version").GetInt32().Should().Be(2);
        InputKilograms(before).Should().Be(firstMaterial.InputKilograms);
        InputKilograms(updatedAfter).Should().Be(firstMaterial.InputKilograms + 100);
    }

    private static async Task<Api.Data.Entities.ObligationCalculation> SaveWithCorrelation(
        ObligationCalculationService service,
        Api.Data.Entities.ObligationCalculation calculation
    )
    {
        using var correlation = CorrelationContext.Begin(NullLogger.Instance, TraceId);

        return await service.Save(calculation, TestContext.Current.CancellationToken);
    }

    private static Func<JsonElement, bool> MatchEntityEvent(string entityId, string operation, string eventType) =>
        root =>
            root.GetProperty("entityId").GetString() == entityId
            && root.GetProperty("operation").GetString() == operation
            && root.GetProperty("eventType").GetString() == eventType;

    private static long InputKilograms(JsonElement calculation) =>
        calculation.GetProperty("producers")[0].GetProperty("materials")[0].GetProperty("inputKilograms").GetInt64();

    private static void AssertCalculationEnvelope(JsonElement root, string entityId, int version)
    {
        root.GetProperty("eventId").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("entity").GetString().Should().Be("obligation_calculation");
        root.GetProperty("entityId").GetString().Should().Be(entityId);
        root.GetProperty("actor").GetString().Should().Be("system:waste-obligations");
        root.GetProperty("deletedReason").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("piiKeyRef").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("correlationId").GetString().Should().Be(TraceId);
        root.GetProperty("version").GetInt32().Should().Be(version);
        root.GetProperty("schemaVersion").GetString().Should().Be("obligation_calculation_v1.0");
        var occurredAt = root.GetProperty("occurredAt").GetString();
        var recordedAt = root.GetProperty("recordedAt").GetString();
        const string utcMilliseconds = @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$";
        occurredAt.Should().MatchRegex(utcMilliseconds);
        recordedAt.Should().MatchRegex(utcMilliseconds);
        DateTimeOffset
            .Parse(recordedAt, CultureInfo.InvariantCulture)
            .Should()
            .BeOnOrAfter(DateTimeOffset.Parse(occurredAt, CultureInfo.InvariantCulture));
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
