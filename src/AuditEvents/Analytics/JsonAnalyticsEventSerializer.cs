using System.Text.Json;

namespace Defra.WasteObligations.AuditEvents.Analytics;

public class JsonAnalyticsEventSerializer : IAnalyticsEventSerializer
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new BsonDocumentJsonConverter() },
    };

    public string Serialize(AnalyticsEvent analyticsEvent) =>
        JsonSerializer.Serialize(analyticsEvent, JsonSerializerOptions);
}
