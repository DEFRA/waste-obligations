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
    public async Task Serialize_WhenDeleteWithBefore_ShouldSerializeAsJson()
    {
        var subject = CreateSubject();
        var analyticsEvent = AnalyticsEventFixture
            .ComplianceDeclaration("01JZ8RXBMTY2K15SJB3PCFN3D7", 125)
            .With(x => x.EntityId, "compliance_declaration_65f1f6570bb08052a8a27b01")
            .With(x => x.Operation, "delete")
            .With(x => x.OccurredAt, new DateTimeOffset(2026, 1, 2, 3, 5, 5, TimeSpan.Zero))
            .With(x => x.RecordedAt, new DateTimeOffset(2026, 1, 2, 3, 5, 6, TimeSpan.Zero))
            .With(x => x.Actor, "service:waste-obligations")
            .With(x => x.Version, 2)
            .With(x => x.Before, ComplianceDeclarationDocument(ComplianceDeclarationStatus.Submitted))
            .With(x => x.SchemaVersion, AnalyticsEventSchemaVersion)
            .Create();

        var result = subject.Serialize(analyticsEvent);

        await VerifyJson(result).DontScrubDateTimes().DontScrubGuids();
    }

    [Fact]
    public void Serialize_WhenDateSerializationIsNotSpecified_ShouldSerializeDateTimeValue()
    {
        var subject = new JsonAnalyticsEventSerializer(
            new InlineSchemaProvider(
                """
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
                """
            )
        );
        var analyticsEvent = AnalyticsEventFixture
            .Default("01JZ8RXBMTY2K15SJB3PCFN3D8", 126)
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

    [Fact]
    public void Serialize_WhenBeforeOrAfterIsNotABsonDocument_ShouldThrow()
    {
        var subject = new JsonAnalyticsEventSerializer(
            new InlineSchemaProvider("""{ "type": "object", "properties": {} }""")
        );
        var analyticsEvent = AnalyticsEventFixture
            .Default("01JZ8RXBMTY2K15SJB3PCFN3D9", 127)
            .With(x => x.Entity, "test_entity")
            .With(x => x.SchemaVersion, "test_entity.v1.0")
            .With(x => x.Before, "not a BSON document")
            .Create();

        var act = () => subject.Serialize(analyticsEvent);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("AnalyticsEvent before and after values must be BsonDocument instances.");
    }

    [Fact]
    public void Serialize_WhenBsonDocumentContainsSchemaBoundValues_ShouldSerializeUsingSchema()
    {
        var subject = new JsonAnalyticsEventSerializer(
            new InlineSchemaProvider(
                """
                {
                  "type": "object",
                  "properties": {
                    "id": { "type": "string", "x-bson-name": "_id" },
                    "doubleValue": { "type": "number" },
                    "longValue": { "type": "integer" },
                    "bytes": { "type": "string" },
                    "absent": { "type": "string" },
                    "choice": {
                      "oneOf": [
                        { "type": "string" },
                        { "type": "integer" }
                      ]
                    },
                    "nested": {
                      "oneOf": [
                        {
                          "type": "object",
                          "required": [ "known" ],
                          "properties": {
                            "known": { "type": "string" }
                          }
                        },
                        {
                          "type": "object",
                          "required": [ "other" ],
                          "properties": {
                            "other": { "type": "string" }
                          }
                        }
                      ]
                    },
                    "fallbackNested": {
                      "oneOf": [
                        {
                          "type": "object",
                          "required": [ "missing" ],
                          "properties": {
                            "missing": { "type": "string" }
                          }
                        },
                        {
                          "type": "object",
                          "required": [ "alsoMissing" ],
                          "properties": {
                            "alsoMissing": { "type": "string" }
                          }
                        }
                      ]
                    }
                  }
                }
                """
            )
        );
        var analyticsEvent = AnalyticsEventFixture
            .Default("01JZ8RXBMTY2K15SJB3PCFN3DA", 128)
            .With(x => x.Entity, "test_entity")
            .With(x => x.SchemaVersion, "test_entity.v1.0")
            .With(
                x => x.After,
                new BsonDocument
                {
                    ["_id"] = s_complianceDeclarationId,
                    ["doubleValue"] = 1.25,
                    ["longValue"] = 1234567890123L,
                    ["bytes"] = new BsonBinaryData([1, 2, 3]),
                    ["choice"] = "selected",
                    ["nested"] = new BsonDocument { ["known"] = "present", ["other"] = "ignored" },
                    ["fallbackNested"] = new BsonDocument { ["unknown"] = "ignored" },
                }
            )
            .Create();

        var result = subject.Serialize(analyticsEvent);
        using var document = JsonDocument.Parse(result);
        var after = document.RootElement.GetProperty("after");

        after.GetProperty("id").GetString().Should().Be(s_complianceDeclarationId.ToString());
        after.GetProperty("doubleValue").GetDouble().Should().Be(1.25);
        after.GetProperty("longValue").GetInt64().Should().Be(1234567890123L);
        after.GetProperty("bytes").GetString().Should().Be("AQID");
        after.TryGetProperty("absent", out _).Should().BeFalse();
        after.GetProperty("choice").GetString().Should().Be("selected");
        after.GetProperty("nested").EnumerateObject().Should().ContainSingle(x => x.Name == "known");
        after.GetProperty("fallbackNested").EnumerateObject().Should().BeEmpty();
    }

    [Fact]
    public void ResolveLocalReference_WhenReferenceIsNotLocalDefinition_ShouldThrow()
    {
        using var document = JsonDocument.Parse("""{ "$defs": {} }""");
        using var reference = JsonDocument.Parse("""{ "$ref": "https://example.com/schema.json" }""");
        var schema = new JsonSchemaDocument(document.RootElement.Clone());

        var act = () => schema.ResolveLocalReference(reference.RootElement);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Unsupported schema reference 'https://example.com/schema.json'.");
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

    private sealed class InlineSchemaProvider(string schema) : IEntityJsonSchemaProvider
    {
        public JsonSchemaDocument Get(string entity, string schemaVersion)
        {
            using var document = JsonDocument.Parse(schema);

            return new JsonSchemaDocument(document.RootElement.Clone());
        }
    }
}
