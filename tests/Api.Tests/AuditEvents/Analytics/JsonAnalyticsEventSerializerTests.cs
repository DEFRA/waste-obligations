using Defra.WasteObligations.AuditEvents.Analytics;
using MongoDB.Bson;

namespace Defra.WasteObligations.Api.Tests.AuditEvents.Analytics;

public class JsonAnalyticsEventSerializerTests
{
    [Fact]
    public async Task Serialize_WhenInsertWithAfter_ShouldSerializeAsJson()
    {
        var subject = new JsonAnalyticsEventSerializer();
        var analyticsEvent = new AnalyticsEvent
        {
            EventId = "01JZ8RXBMTY2K15SJB3PCFN3D5",
            Sequence = 123,
            Entity = "compliance_declaration",
            EntityId = "compliance_declaration_65f1f6570bb08052a8a27b01",
            Operation = "insert",
            OccurredAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            RecordedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 6, TimeSpan.Zero),
            Actor = "service:waste-obligations",
            Version = 1,
            After = new BsonDocument
            {
                ["_id"] = ObjectId.Parse("65f1f6570bb08052a8a27b01"),
                ["created"] = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                ["isRegulation43Compliant"] = true,
                ["obligationYear"] = 2026,
                ["obligations"] = new BsonArray
                {
                    new BsonDocument
                    {
                        ["material"] = "Plastic",
                        ["recyclingTarget"] = 0.75,
                        ["tonnages"] = new BsonDocument { ["accepted"] = 2, ["awaitingAcceptance"] = 10 },
                    },
                },
            },
            SchemaVersion = "compliance_declaration.v1",
        };

        var result = subject.Serialize(analyticsEvent);

        await VerifyJson(result).DontScrubDateTimes();
    }

    [Fact]
    public async Task Serialize_WhenUpdateWithBeforeAndAfter_ShouldSerializeAsJson()
    {
        var subject = new JsonAnalyticsEventSerializer();
        var analyticsEvent = new AnalyticsEvent
        {
            EventId = "01JZ8RXBMTY2K15SJB3PCFN3D6",
            Sequence = 124,
            Entity = "compliance_declaration",
            EntityId = "compliance_declaration_65f1f6570bb08052a8a27b01",
            Operation = "update",
            OccurredAt = new DateTimeOffset(2026, 1, 2, 3, 5, 5, TimeSpan.Zero),
            RecordedAt = new DateTimeOffset(2026, 1, 2, 3, 5, 6, TimeSpan.Zero),
            Actor = "service:waste-obligations",
            Version = 2,
            Before = new BsonDocument
            {
                ["_id"] = ObjectId.Parse("65f1f6570bb08052a8a27b01"),
                ["obligationYear"] = 2026,
                ["status"] = "Submitted",
                ["updated"] = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            },
            After = new BsonDocument
            {
                ["_id"] = ObjectId.Parse("65f1f6570bb08052a8a27b01"),
                ["obligationYear"] = 2027,
                ["status"] = "Submitted",
                ["updated"] = new DateTime(2026, 1, 2, 3, 5, 5, DateTimeKind.Utc),
            },
            SchemaVersion = "compliance_declaration.v1",
        };

        var result = subject.Serialize(analyticsEvent);

        await VerifyJson(result).DontScrubDateTimes();
    }
}
