using System.Text.Json;
using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.IO;

namespace Defra.WasteObligations.Api.Serialization;

public class BsonDocumentJsonConverter : JsonConverter<BsonDocument>
{
    public override BsonDocument Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);

        return BsonDocument.Parse(document.RootElement.GetRawText());
    }

    public override void Write(Utf8JsonWriter writer, BsonDocument value, JsonSerializerOptions options)
    {
        var settings = new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson };
        using var document = JsonDocument.Parse(value.ToJson(settings));
        document.RootElement.WriteTo(writer);
    }
}
