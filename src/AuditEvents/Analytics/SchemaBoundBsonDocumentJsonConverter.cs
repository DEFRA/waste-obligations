using System.Text.Json;
using System.Text.Json.Serialization;
using MongoDB.Bson;

namespace Defra.WasteObligations.AuditEvents.Analytics;

internal sealed class SchemaBoundBsonDocumentJsonConverter : JsonConverter<SchemaBoundBsonDocument>
{
    private const string Id = "id";
    private const string MongoId = "_id";

    public override SchemaBoundBsonDocument Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) => throw new NotSupportedException($"{nameof(SchemaBoundBsonDocumentJsonConverter)} only supports writing.");

    public override void Write(Utf8JsonWriter writer, SchemaBoundBsonDocument value, JsonSerializerOptions options)
    {
        WriteDocument(writer, value.Document, value.Schema.RootElement, value.Schema);
    }

    private static void WriteDocument(
        Utf8JsonWriter writer,
        BsonDocument document,
        JsonElement schemaElement,
        JsonSchemaDocument schema
    )
    {
        var resolvedSchema = schema.ResolveLocalReference(schemaElement);

        writer.WriteStartObject();

        if (resolvedSchema.TryGetProperty("properties", out var properties))
        {
            foreach (var property in properties.EnumerateObject())
            {
                if (!TryGetDocumentValue(document, property.Name, out var value))
                {
                    continue;
                }

                writer.WritePropertyName(property.Name);
                WriteValue(writer, value, property.Value, schema);
            }
        }

        writer.WriteEndObject();
    }

    private static void WriteValue(
        Utf8JsonWriter writer,
        BsonValue value,
        JsonElement schemaElement,
        JsonSchemaDocument schema
    )
    {
        if (value.IsBsonNull)
        {
            writer.WriteNullValue();

            return;
        }

        var resolvedSchema = schema.ResolveLocalReference(schemaElement);

        if (resolvedSchema.TryGetProperty("oneOf", out var oneOf))
        {
            resolvedSchema = ResolveOneOf(value, oneOf, schema);
        }

        switch (value.BsonType)
        {
            case BsonType.Array:
                WriteArray(writer, value.AsBsonArray, resolvedSchema, schema);
                break;
            case BsonType.Binary:
                WriteBinary(writer, value.AsBsonBinaryData);
                break;
            case BsonType.Boolean:
                writer.WriteBooleanValue(value.AsBoolean);
                break;
            case BsonType.DateTime:
                WriteDateTime(writer, value, resolvedSchema);
                break;
            case BsonType.Decimal128:
                writer.WriteNumberValue(value.AsDecimal);
                break;
            case BsonType.Document:
                WriteDocument(writer, value.AsBsonDocument, resolvedSchema, schema);
                break;
            case BsonType.Double:
                writer.WriteNumberValue(value.AsDouble);
                break;
            case BsonType.Int32:
                writer.WriteNumberValue(value.AsInt32);
                break;
            case BsonType.Int64:
                writer.WriteNumberValue(value.AsInt64);
                break;
            case BsonType.ObjectId:
                writer.WriteStringValue(value.AsObjectId.ToString());
                break;
            case BsonType.String:
                writer.WriteStringValue(value.AsString);
                break;
            default:
                JsonSerializer.Serialize(writer, BsonTypeMapper.MapToDotNetValue(value));
                break;
        }
    }

    private static void WriteArray(
        Utf8JsonWriter writer,
        BsonArray array,
        JsonElement schemaElement,
        JsonSchemaDocument schema
    )
    {
        var itemSchema = schemaElement.TryGetProperty("items", out var items) ? items : schemaElement;

        writer.WriteStartArray();

        foreach (var item in array)
        {
            WriteValue(writer, item, itemSchema, schema);
        }

        writer.WriteEndArray();
    }

    private static void WriteBinary(Utf8JsonWriter writer, BsonBinaryData value)
    {
        if (value.SubType is BsonBinarySubType.UuidStandard)
        {
            writer.WriteStringValue(value.ToGuid(GuidRepresentation.Standard).ToString());

            return;
        }

        writer.WriteStringValue(Convert.ToBase64String(value.Bytes));
    }

    private static void WriteDateTime(Utf8JsonWriter writer, BsonValue value, JsonElement schemaElement)
    {
        var dateTime = value.ToUniversalTime();

        if (
            schemaElement.TryGetProperty("x-date-serialization", out var serialization)
            && serialization.GetString() is "date-time-offset"
        )
        {
            writer.WriteStringValue(new DateTimeOffset(dateTime));

            return;
        }

        writer.WriteStringValue(dateTime);
    }

    private static JsonElement ResolveOneOf(BsonValue value, JsonElement oneOf, JsonSchemaDocument schema)
    {
        if (value.BsonType is not BsonType.Document)
        {
            return oneOf.EnumerateArray().First();
        }

        var document = value.AsBsonDocument;

        foreach (
            var candidate in oneOf
                .EnumerateArray()
                .Select(schema.ResolveLocalReference)
                .OrderByDescending(RequiredCount)
        )
        {
            if (!candidate.TryGetProperty("required", out var required))
            {
                return candidate;
            }

            if (required.EnumerateArray().All(x => HasDocumentValue(document, x.GetString()!)))
            {
                return candidate;
            }
        }

        return oneOf.EnumerateArray().Select(schema.ResolveLocalReference).OrderByDescending(RequiredCount).First();
    }

    private static int RequiredCount(JsonElement schema) =>
        schema.TryGetProperty("required", out var required) ? required.GetArrayLength() : 0;

    private static bool TryGetDocumentValue(BsonDocument document, string propertyName, out BsonValue value)
    {
        if (document.TryGetValue(propertyName, out value))
        {
            return true;
        }

        if (propertyName is Id && document.TryGetValue(MongoId, out value))
        {
            return true;
        }

        value = BsonNull.Value;

        return false;
    }

    private static bool HasDocumentValue(BsonDocument document, string propertyName) =>
        document.Contains(propertyName) || propertyName is Id && document.Contains(MongoId);
}

internal sealed record SchemaBoundBsonDocument(BsonDocument Document, JsonSchemaDocument Schema);
