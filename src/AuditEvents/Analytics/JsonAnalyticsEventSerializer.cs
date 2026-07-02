using System.Text.Json;
using MongoDB.Bson;

namespace Defra.WasteObligations.AuditEvents.Analytics;

public class JsonAnalyticsEventSerializer : IAnalyticsEventSerializer
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new SchemaBoundBsonDocumentJsonConverter() },
    };

    private readonly IEntityJsonSchemaProvider _schemaProvider;

    public JsonAnalyticsEventSerializer(IEntityJsonSchemaProvider schemaProvider)
    {
        _schemaProvider = schemaProvider;
    }

    public string Serialize(AnalyticsEvent analyticsEvent)
    {
        var schema = _schemaProvider.Get(analyticsEvent.Entity, analyticsEvent.SchemaVersion);
        var schemaBoundAnalyticsEvent = analyticsEvent with
        {
            Before = CreateSchemaBoundDocument(analyticsEvent.Before, schema),
            After = CreateSchemaBoundDocument(analyticsEvent.After, schema),
        };

        return JsonSerializer.Serialize(schemaBoundAnalyticsEvent, JsonSerializerOptions);
    }

    private static SchemaBoundBsonDocument? CreateSchemaBoundDocument(object? value, JsonSchemaDocument schema)
    {
        if (value is null)
        {
            return null;
        }

        if (value is BsonDocument document)
        {
            return new SchemaBoundBsonDocument(document, schema);
        }

        throw new InvalidOperationException(
            $"{nameof(AnalyticsEvent)} before and after values must be {nameof(BsonDocument)} instances."
        );
    }
}
