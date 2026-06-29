using System.Text.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Schemas;
using Defra.WasteObligations.AuditEvents.Analytics;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using MongoDB.Bson;

namespace Defra.WasteObligations.Api.Tests.AuditEvents.Analytics;

public class JsonAnalyticsEventSerializerTests
{
    private const string Entity = "compliance_declaration";
    private const string AnalyticsEventSchemaVersion = $"{Entity}.{ComplianceDeclaration.SchemaVersionValue}";
    private static readonly ObjectId s_complianceDeclarationId = ObjectId.Parse("65f1f6570bb08052a8a27b01");
    private static readonly Guid s_organisationId = Guid.Parse("5dbef606-3611-42f4-b39f-cad828badc12");
    private static readonly DateTime s_submittedAt = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    private static readonly DateTime s_acceptedAt = new(2026, 1, 2, 3, 5, 5, DateTimeKind.Utc);

    static JsonAnalyticsEventSerializerTests()
    {
        Api.Data.ServiceCollectionExtensions.RegisterConventions();
    }

    [Fact]
    public async Task Serialize_WhenInsertWithAfter_ShouldSerializeAsJson()
    {
        var subject = CreateSubject();
        var analyticsEvent = AnalyticsEventFixture
            .ComplianceDeclaration("01JZ8RXBMTY2K15SJB3PCFN3D5", 123)
            .With(x => x.EntityId, "compliance_declaration_65f1f6570bb08052a8a27b01")
            .With(x => x.OccurredAt, new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero))
            .With(x => x.RecordedAt, new DateTimeOffset(2026, 1, 2, 3, 4, 6, TimeSpan.Zero))
            .With(x => x.Actor, "service:waste-obligations")
            .With(x => x.After, ComplianceDeclarationDocument(ComplianceDeclarationStatus.Submitted))
            .With(x => x.SchemaVersion, AnalyticsEventSchemaVersion)
            .Create();

        var result = subject.Serialize(analyticsEvent);

        await VerifyJson(result).DontScrubDateTimes().DontScrubGuids();
    }

    [Fact]
    public async Task Serialize_WhenUpdateWithBeforeAndAfter_ShouldSerializeAsJson()
    {
        var subject = CreateSubject();
        var analyticsEvent = AnalyticsEventFixture
            .ComplianceDeclaration("01JZ8RXBMTY2K15SJB3PCFN3D6", 124)
            .With(x => x.EntityId, "compliance_declaration_65f1f6570bb08052a8a27b01")
            .With(x => x.Operation, "update")
            .With(x => x.OccurredAt, new DateTimeOffset(2026, 1, 2, 3, 5, 5, TimeSpan.Zero))
            .With(x => x.RecordedAt, new DateTimeOffset(2026, 1, 2, 3, 5, 6, TimeSpan.Zero))
            .With(x => x.Actor, "service:waste-obligations")
            .With(x => x.Version, 2)
            .With(x => x.Before, ComplianceDeclarationDocument(ComplianceDeclarationStatus.Submitted))
            .With(x => x.After, ComplianceDeclarationDocument(ComplianceDeclarationStatus.Accepted))
            .With(x => x.SchemaVersion, AnalyticsEventSchemaVersion)
            .Create();

        var result = subject.Serialize(analyticsEvent);

        await VerifyJson(result).DontScrubDateTimes().DontScrubGuids();
    }

    [Fact]
    public void Serialize_WhenDateSerializationIsNotSpecified_ShouldSerializeDateTimeValue()
    {
        var subject = new JsonAnalyticsEventSerializer(new InlineSchemaProvider());
        var analyticsEvent = AnalyticsEventFixture
            .Default("01JZ8RXBMTY2K15SJB3PCFN3D7", 125)
            .With(x => x.Entity, "test_entity")
            .With(x => x.EntityId, "test_entity_65f1f6570bb08052a8a27b01")
            .With(x => x.OccurredAt, new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero))
            .With(x => x.RecordedAt, new DateTimeOffset(2026, 1, 2, 3, 4, 6, TimeSpan.Zero))
            .With(x => x.Actor, "service:waste-obligations")
            .With(
                x => x.After,
                new BsonDocument
                {
                    ["dateWithOffset"] = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                    ["dateWithoutOffset"] = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                }
            )
            .With(x => x.SchemaVersion, "test_entity.v1.0")
            .Create();

        var result = subject.Serialize(analyticsEvent);
        using var document = JsonDocument.Parse(result);
        var after = document.RootElement.GetProperty("after");

        after.GetProperty("dateWithOffset").GetString().Should().Be("2026-01-02T03:04:05+00:00");
        after.GetProperty("dateWithoutOffset").GetString().Should().Be("2026-01-02T03:04:05Z");
    }

    private static BsonDocument ComplianceDeclarationDocument(ComplianceDeclarationStatus status)
    {
        var submitted = ComplianceDeclarationFixture
            .DirectProducer(s_organisationId)
            .With(x => x.Id, s_complianceDeclarationId)
            .With(x => x.Created, s_submittedAt)
            .With(x => x.Updated, s_submittedAt)
            .With(x => x.Audit, AuditEntryFixture.Submitted(s_submittedAt))
            .Create();

        var declaration =
            status is ComplianceDeclarationStatus.Submitted
                ? submitted
                : submitted.UpdateStatus(status, "Accepted reason", UserFixture.Default().Create(), s_acceptedAt) with
                {
                    Version = 2,
                    Updated = s_acceptedAt,
                };

        var document = declaration.ToBsonDocument();

        return document;
    }

    private static IAnalyticsEventSerializer CreateSubject()
    {
        return new JsonAnalyticsEventSerializer(new EmbeddedEntityJsonSchemaProvider());
    }

    private sealed class InlineSchemaProvider : IEntityJsonSchemaProvider
    {
        public JsonSchemaDocument Get(string entity, string schemaVersion)
        {
            const string schema = """
                {
                  "type": "object",
                  "properties": {
                    "dateWithOffset": {
                      "type": "string",
                      "format": "date-time",
                      "x-date-serialization": "date-time-offset"
                    },
                    "dateWithoutOffset": {
                      "type": "string",
                      "format": "date-time"
                    }
                  }
                }
                """;

            using var document = JsonDocument.Parse(schema);

            return new JsonSchemaDocument(document.RootElement.Clone());
        }
    }
}
